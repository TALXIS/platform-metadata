namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Describes an attribute declared in a schema.
/// </summary>
public sealed class SchemaAttribute
{
    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether the attribute is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the simplified schema type name, when known.
    /// </summary>
    public string? TypeName { get; }

    /// <summary>
    /// Gets allowed values for enum-like attributes, when known.
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; }

    /// <summary>
    /// Creates a schema attribute description.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="required">Whether the attribute is required.</param>
    /// <param name="typeName">Simplified schema type name, when known.</param>
    /// <param name="allowedValues">Allowed values for enum-like attributes, when known.</param>
    public SchemaAttribute(string name, bool required, string? typeName, IReadOnlyList<string>? allowedValues)
    {
        Name = name;
        Required = required;
        TypeName = typeName;
        AllowedValues = allowedValues;
    }
}
