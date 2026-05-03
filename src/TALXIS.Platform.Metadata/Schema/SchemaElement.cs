namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes an element declared in a schema, including its children and attributes.
/// </summary>
public sealed class SchemaElement
{
    public string Name { get; }
    public bool Required { get; }
    public int? MaxOccurs { get; }
    public string? TypeName { get; }
    public IReadOnlyList<string>? AllowedValues { get; }
    public IReadOnlyList<SchemaElement>? Children { get; }
    public IReadOnlyList<SchemaAttribute>? Attributes { get; }

    public SchemaElement(
        string name,
        bool required,
        int? maxOccurs,
        string? typeName,
        IReadOnlyList<string>? allowedValues,
        IReadOnlyList<SchemaElement>? children,
        IReadOnlyList<SchemaAttribute>? attributes)
    {
        Name = name;
        Required = required;
        MaxOccurs = maxOccurs;
        TypeName = typeName;
        AllowedValues = allowedValues;
        Children = children;
        Attributes = attributes;
    }
}
