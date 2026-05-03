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
    private readonly List<JSchema> _schemas = new();

    /// <summary>
    /// Creates a validator pre-loaded with all embedded JSON schemas.
    /// </summary>
    public JsonValidator()
    {
        foreach (var name in SchemaResourceLoader.GetAvailableSchemas().Where(n => n.EndsWith(".json")))
        {
            using var stream = SchemaResourceLoader.OpenSchema(name);
            using var reader = new StreamReader(stream);
            _schemas.Add(JSchema.Parse(reader.ReadToEnd()));
        }
    }

    /// <summary>
    /// Validates a JSON file against all loaded schemas.
    /// A file passes if it is valid against at least one schema.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new[] { new ValidationResult(ValidationSeverity.Error, $"File not found: {filePath}", filePath, null, null) };

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

        if (_schemas.Count == 0)
            return System.Array.Empty<ValidationResult>();

        // File passes if valid against ANY schema
        var allErrors = new List<ValidationResult>();
        foreach (var schema in _schemas)
        {
            var schemaErrors = ValidateAgainstSchema(token, schema, filePath);
            if (schemaErrors.Count == 0)
                return System.Array.Empty<ValidationResult>();

            allErrors.AddRange(schemaErrors);
        }

        return allErrors;
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
            var line = GetLineNumber(token, error);
            var column = GetLinePosition(token, error);
            results.Add(new ValidationResult(ValidationSeverity.Error, error.Message, filePath, line, column));
        });

        return results;
    }

    private static int? GetLineNumber(JToken root, ValidationError error)
    {
        var line = TryGetLineInfo(error);
        if (line.lineNumber.HasValue)
            return line.lineNumber;

        var byPath = TryGetLineInfo(root, error.Path);
        if (byPath.lineNumber.HasValue)
            return byPath.lineNumber;

        return error.ChildErrors.Select(e => GetLineNumber(root, e)).FirstOrDefault(x => x.HasValue);
    }

    private static int? GetLinePosition(JToken root, ValidationError error)
    {
        var line = TryGetLineInfo(error);
        if (line.linePosition.HasValue)
            return line.linePosition;

        var byPath = TryGetLineInfo(root, error.Path);
        if (byPath.linePosition.HasValue)
            return byPath.linePosition;

        return error.ChildErrors.Select(e => GetLinePosition(root, e)).FirstOrDefault(x => x.HasValue);
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
