namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes an element declared in a schema, including its children and attributes.
/// </summary>
public sealed class SchemaElement
{
    /// <summary>
    /// Gets the element name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether the element is required in its current context.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the maximum occurrence count; <see langword="null"/> represents an unbounded maximum.
    /// </summary>
    public int? MaxOccurs { get; }

    /// <summary>
    /// Gets the simplified schema type name, when known.
    /// </summary>
    public string? TypeName { get; }

    /// <summary>
    /// Gets allowed values for enum-like elements, when known.
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; }

    /// <summary>
    /// Gets child elements for complex elements, when known.
    /// </summary>
    public IReadOnlyList<SchemaElement>? Children { get; }

    /// <summary>
    /// Gets attributes for complex elements, when known.
    /// </summary>
    public IReadOnlyList<SchemaAttribute>? Attributes { get; }

    /// <summary>
    /// Creates a schema element description.
    /// </summary>
    /// <param name="name">Element name.</param>
    /// <param name="required">Whether the element is required in its current context.</param>
    /// <param name="maxOccurs">Maximum occurrence count; <see langword="null"/> represents unbounded.</param>
    /// <param name="typeName">Simplified schema type name, when known.</param>
    /// <param name="allowedValues">Allowed values for enum-like elements, when known.</param>
    /// <param name="children">Child elements for complex elements, when known.</param>
    /// <param name="attributes">Attributes for complex elements, when known.</param>
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
