using System.IO;
using System.Collections.Generic;
using System.Linq;
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
            token = JToken.Parse(File.ReadAllText(filePath));
        }
        catch (System.Exception ex)
        {
            return new[] { new ValidationResult(ValidationSeverity.Error, $"Invalid JSON: {ex.Message}", filePath, null, null) };
        }

        if (_schemas.Count == 0)
            return System.Array.Empty<ValidationResult>();

        // File passes if valid against ANY schema
        var allErrors = new List<ValidationResult>();
        foreach (var schema in _schemas)
        {
            if (token.IsValid(schema, out IList<string> errors))
                return System.Array.Empty<ValidationResult>();

            allErrors.AddRange(errors.Select(e =>
                new ValidationResult(ValidationSeverity.Error, e, filePath, null, null)));
        }

        return allErrors;
    }
}
