using System.IO;
using Newtonsoft.Json.Schema;
using TALXIS.Platform.Metadata.Schema;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Walks Newtonsoft JSchema graphs to produce <see cref="ComponentSchema"/> trees,
/// mirroring the approach used by <see cref="XsdSchemaIntrospector"/> for XSD files.
/// </summary>
public sealed class JsonSchemaIntrospector : ISchemaIntrospector
{
    private const int MaxDepth = 10;

    /// <summary>
    /// Maps lowered schema-file stem (e.g. "flow") → parsed JSchema.
    /// </summary>
    private readonly Dictionary<string, (string FileName, JSchema Schema)> _schemas = new(StringComparer.OrdinalIgnoreCase);

    public JsonSchemaIntrospector()
    {
        foreach (var name in SchemaResourceLoader.GetAvailableSchemas()
                     .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = SchemaResourceLoader.OpenSchema(name);
            using var reader = new StreamReader(stream);
            var schema = JSchema.Parse(reader.ReadToEnd());

            // "Flow.schema.json" → stem "Flow"
            var stem = name;
            var dotIndex = stem.IndexOf('.');
            if (dotIndex > 0)
                stem = stem.Substring(0, dotIndex);

            _schemas[stem] = (name, schema);
        }
    }

    /// <inheritdoc />
    public ComponentSchema? GetSchema(string componentName)
    {
        if (!_schemas.TryGetValue(componentName, out var entry))
            return null;

        var (fileName, schema) = entry;

        // Use the file stem as the canonical root element name
        var rootName = fileName;
        var dotIndex = rootName.IndexOf('.');
        if (dotIndex > 0)
            rootName = rootName.Substring(0, dotIndex);

        var elements = WalkProperties(schema, depth: 0);
        return new ComponentSchema(rootName, elements, Array.Empty<SchemaAttribute>());
    }

    private IReadOnlyList<SchemaElement> WalkProperties(JSchema schema, int depth)
    {
        if (depth >= MaxDepth)
            return Array.Empty<SchemaElement>();

        var elements = new List<SchemaElement>();
        var requiredSet = schema.Required ?? (IList<string>)Array.Empty<string>();

        foreach (var kvp in schema.Properties)
        {
            var name = kvp.Key;
            var child = kvp.Value;
            var isRequired = requiredSet.Contains(name);

            elements.Add(BuildElement(name, child, isRequired, depth));
        }

        return elements;
    }

    private SchemaElement BuildElement(string name, JSchema schema, bool required, int depth)
    {
        var typeName = MapType(schema);
        var allowedValues = GetAllowedValues(schema);

        IReadOnlyList<SchemaElement>? children = null;
        int? maxOccurs = null;

        if (schema.Type == JSchemaType.Object || schema.Properties.Count > 0)
        {
            children = WalkProperties(schema, depth + 1);
        }
        else if (schema.Type == JSchemaType.Array && schema.Items.Count > 0)
        {
            maxOccurs = int.MaxValue;
            // Walk the first item schema as children
            var itemSchema = schema.Items[0];
            if (itemSchema.Properties.Count > 0)
            {
                children = WalkProperties(itemSchema, depth + 1);
            }
        }

        return new SchemaElement(
            name,
            required,
            maxOccurs,
            typeName,
            allowedValues,
            children,
            null // JSON Schema has no attribute concept
        );
    }

    private static string MapType(JSchema schema)
    {
        if (schema.Enum != null && schema.Enum.Count > 0)
            return "enum";

        return schema.Type switch
        {
            JSchemaType.String  => "string",
            JSchemaType.Integer => "integer",
            JSchemaType.Number  => "number",
            JSchemaType.Boolean => "boolean",
            JSchemaType.Object  => "object",
            JSchemaType.Array   => "array",
            _                   => "string"
        };
    }

    private static IReadOnlyList<string>? GetAllowedValues(JSchema schema)
    {
        if (schema.Enum == null || schema.Enum.Count == 0)
            return null;

        return schema.Enum.Select(e => e?.ToString() ?? string.Empty).ToList();
    }
}
