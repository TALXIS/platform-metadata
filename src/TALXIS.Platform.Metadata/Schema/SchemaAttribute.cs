namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes an attribute declared in a schema.
/// </summary>
public sealed class SchemaAttribute
{
    public string Name { get; }
    public bool Required { get; }
    public string? TypeName { get; }
    public IReadOnlyList<string>? AllowedValues { get; }

    public SchemaAttribute(string name, bool required, string? typeName, IReadOnlyList<string>? allowedValues)
    {
        Name = name;
        Required = required;
        TypeName = typeName;
        AllowedValues = allowedValues;
    }
}
