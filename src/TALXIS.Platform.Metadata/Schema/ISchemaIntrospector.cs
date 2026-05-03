namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Introspects component schemas to discover expected structure.
/// Format-specific implementations handle XSD, JSON Schema, etc.
/// </summary>
public interface ISchemaIntrospector
{
    /// <summary>
    /// Gets a schema description for a component or root element name.
    /// </summary>
    /// <param name="componentName">Component or root element name.</param>
    /// <returns>The schema description, or <see langword="null"/> when no matching schema is known.</returns>
    ComponentSchema? GetSchema(string componentName);
}
