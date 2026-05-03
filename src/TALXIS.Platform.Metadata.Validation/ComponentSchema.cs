namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Describes the full schema structure for a root XML element, as extracted from the XSD schema set.
/// </summary>
public sealed class ComponentSchema
{
    public string RootElement { get; }
    public IReadOnlyList<SchemaElement> Elements { get; }
    public IReadOnlyList<SchemaAttribute> Attributes { get; }

    public ComponentSchema(string rootElement, IReadOnlyList<SchemaElement> elements, IReadOnlyList<SchemaAttribute> attributes)
    {
        RootElement = rootElement;
        Elements = elements;
        Attributes = attributes;
    }
}
