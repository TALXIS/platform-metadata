using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates JSON files against embedded JSON Schema definitions.
/// </summary>
public sealed class JsonValidator
{
    private readonly Dictionary<string, JSchema> _routedSchemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<JSchema> _explicitSchemas = new();
    private readonly bool _useRouting;

    /// <summary>
    /// Creates a validator pre-loaded with all embedded JSON schemas. Each file is
    /// validated only against the schema that applies to it (resolved by file name
    /// and location); files that match no schema are left untouched.
    /// </summary>
    public JsonValidator()
    {
        foreach (var name in SchemaResourceLoader.GetAvailableSchemas().Where(n => n.EndsWith(".json")))
        {
            using var stream = SchemaResourceLoader.OpenSchema(name);
            using var reader = new StreamReader(stream);
            var dotIndex = name.IndexOf('.');
            var stem = dotIndex > 0 ? name.Substring(0, dotIndex) : name;
            _routedSchemas[stem] = JSchema.Parse(reader.ReadToEnd());
        }

        _useRouting = true;
    }

    internal JsonValidator(IEnumerable<JSchema> schemas)
    {
        if (schemas is null)
            throw new ArgumentNullException(nameof(schemas));

        _explicitSchemas.AddRange(schemas);
        _useRouting = false;
    }

    /// <summary>
    /// Validates a JSON file against the schema that applies to it.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new[] { new ValidationResult(ValidationSeverity.Error, $"File not found: {filePath}", filePath, null, null) };

        var schemas = _useRouting ? ResolveSchemas(filePath) : _explicitSchemas;
        if (schemas.Count == 0)
            return System.Array.Empty<ValidationResult>();

        JToken token;
        try
        {
            token = LoadToken(filePath);
        }
        catch (JsonReaderException ex)
        {
            return new[]
            {
                new ValidationResult(
                    ValidationSeverity.Error,
                    $"Invalid JSON: {ex.Message}",
                    filePath,
                    ex.LineNumber > 0 ? ex.LineNumber : null,
                    ex.LineNumber > 0 ? System.Math.Max(ex.LinePosition, 1) : null)
            };
        }

        // File passes if valid against ANY of the applicable schemas.
        var allErrors = new List<ValidationResult>();
        foreach (var schema in schemas)
        {
            var schemaErrors = ValidateAgainstSchema(token, schema, filePath);
            if (schemaErrors.Count == 0)
                return System.Array.Empty<ValidationResult>();

            allErrors.AddRange(schemaErrors);
        }

        return allErrors;
    }

    /// <summary>
    /// Returns the embedded schemas that apply to <paramref name="filePath"/> based on
    /// its name and location. Returns an empty list for files no schema targets, so
    /// non-metadata JSON (package.json, tsconfig.json, settings.json, ...) is skipped.
    /// </summary>
    private IReadOnlyList<JSchema> ResolveSchemas(string filePath)
    {
        if (_routedSchemas.TryGetValue("Flow", out var flow) && IsFlowDefinition(filePath))
            return new[] { flow };

        return System.Array.Empty<JSchema>();
    }

    private static bool IsFlowDefinition(string filePath)
    {
        var segments = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s.Equals("Workflows", StringComparison.OrdinalIgnoreCase));
    }

    private static JToken LoadToken(string filePath)
    {
        using var textReader = File.OpenText(filePath);
        using var jsonReader = new JsonTextReader(textReader)
        {
            DateParseHandling = DateParseHandling.None
        };

        return JToken.Load(jsonReader, new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load
        });
    }

    private static IReadOnlyList<ValidationResult> ValidateAgainstSchema(JToken token, JSchema schema, string filePath)
    {
        var results = new List<ValidationResult>();

        token.Validate(schema, (_, args) =>
        {
            var error = args.ValidationError;
            var location = ResolveLocation(token, error);
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                error.Message,
                filePath,
                location.lineNumber,
                location.linePosition));
        });

        return results;
    }

    private static (int? lineNumber, int? linePosition) ResolveLocation(JToken root, ValidationError error)
    {
        var location = TryGetLineInfo(error);
        if (location.lineNumber.HasValue || location.linePosition.HasValue)
            return location;

        location = TryGetLineInfo(root, error.Path);
        if (location.lineNumber.HasValue || location.linePosition.HasValue)
            return location;

        foreach (var childError in error.ChildErrors)
        {
            location = ResolveLocation(root, childError);
            if (location.lineNumber.HasValue || location.linePosition.HasValue)
                return location;
        }

        return (null, null);
    }

    private static (int? lineNumber, int? linePosition) TryGetLineInfo(ValidationError error)
    {
        var value = error.Value;
        if (value is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            int? lineNumber = lineInfo.LineNumber > 0 ? lineInfo.LineNumber : null;
            int? linePosition = lineInfo.LinePosition > 0 ? lineInfo.LinePosition : null;
            return (lineNumber, linePosition);
        }

        return (null, null);
    }

    private static (int? lineNumber, int? linePosition) TryGetLineInfo(JToken root, string? path)
    {
        JToken? token;
        if (string.IsNullOrEmpty(path))
            token = root;
        else
        {
            var nonNullPath = path!;
            token = root.SelectToken(nonNullPath, false);
        }

        if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            int? lineNumber = lineInfo.LineNumber > 0 ? lineInfo.LineNumber : null;
            int? linePosition = lineInfo.LinePosition > 0 ? lineInfo.LinePosition : null;
            return (lineNumber, linePosition);
        }

        return (null, null);
    }
}
