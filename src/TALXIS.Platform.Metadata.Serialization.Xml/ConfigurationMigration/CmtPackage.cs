using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.ConfigurationMigration.Data;
using TALXIS.Platform.Metadata.ConfigurationMigration.Schema;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Configuration Migration Tool package: data_schema.xml plus optional data.xml, loaded as one unit by
/// <see cref="CmtPackageXmlReader"/> and written back by <see cref="CmtPackageXmlWriter"/>. Like
/// <see cref="Workspace"/> it keeps the original documents so saving patches them in place.
/// </summary>
public sealed class CmtPackage
{
    /// <summary>Creates a package from in-memory models; saving it writes new files.</summary>
    /// <param name="schema">The schema (data_schema.xml).</param>
    /// <param name="data">The records (data.xml), or <c>null</c> for a schema-only package.</param>
    public CmtPackage(CmtDataSchema schema, CmtData? data = null)
    {
        Schema = schema;
        Data = data;
    }

    /// <summary>The schema (data_schema.xml). Empty when the file could not be loaded; see <see cref="LoadErrors"/>.</summary>
    public CmtDataSchema Schema { get; }

    /// <summary>The records (data.xml), or <c>null</c> when no data file was supplied or it could not be loaded.</summary>
    public CmtData? Data { get; }

    /// <summary>
    /// Files that could not be read or parsed, and records or associations skipped because their id is not a
    /// GUID. A package with load errors is not roundtrip-safe to save: skipped content would be dropped.
    /// </summary>
    public IReadOnlyList<WorkspaceLoadError> LoadErrors { get; internal set; } = Array.Empty<WorkspaceLoadError>();

    internal XDocument? SchemaDocument { get; set; }
    internal XDocument? DataDocument { get; set; }

    // The bytes each document was loaded from: the writer saves an unchanged document as these bytes.
    internal byte[]? SchemaBytes { get; set; }
    internal byte[]? DataBytes { get; set; }
}
