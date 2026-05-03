namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Describes an XML attribute declared in an XSD schema.
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
