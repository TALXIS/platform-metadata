namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes the full schema structure for a root element, as extracted from a schema source.
/// </summary>
public sealed class ComponentSchema
{
    /// <summary>
    /// Gets the root element or schema name this schema describes.
    /// </summary>
    public string RootElement { get; }

    /// <summary>
    /// Gets child elements declared for the root.
    /// </summary>
    public IReadOnlyList<SchemaElement> Elements { get; }

    /// <summary>
    /// Gets attributes declared for the root.
    /// </summary>
    public IReadOnlyList<SchemaAttribute> Attributes { get; }

    /// <summary>
    /// Creates a component schema description.
    /// </summary>
    /// <param name="rootElement">Root element or schema name.</param>
    /// <param name="elements">Child elements declared for the root.</param>
    /// <param name="attributes">Attributes declared for the root.</param>
    public ComponentSchema(string rootElement, IReadOnlyList<SchemaElement> elements, IReadOnlyList<SchemaAttribute> attributes)
    {
        RootElement = rootElement;
        Elements = elements;
        Attributes = attributes;
    }
}
