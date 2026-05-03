namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes the full schema structure for a root element, as extracted from a schema source.
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
