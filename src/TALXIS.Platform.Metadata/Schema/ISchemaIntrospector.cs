namespace TALXIS.Platform.Metadata.Schema;

/// <summary>
/// Introspects component schemas to discover expected structure.
/// Format-specific implementations handle XSD, JSON Schema, etc.
/// </summary>
public interface ISchemaIntrospector
{
    ComponentSchema? GetSchema(string componentName);
}
