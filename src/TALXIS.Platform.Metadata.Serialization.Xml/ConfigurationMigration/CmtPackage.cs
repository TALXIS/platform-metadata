using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Configuration Migration Tool package: data_schema.xml plus optional data.xml.
/// </summary>
public sealed class CmtPackage
{
    public CmtPackage(CmtDataSchema schema, CmtData? data = null, IReadOnlyList<WorkspaceLoadError>? loadErrors = null)
    {
        Schema = schema;
        Data = data;
        LoadErrors = loadErrors ?? Array.Empty<WorkspaceLoadError>();
    }

    public CmtDataSchema Schema { get; }
    public CmtData? Data { get; }
    public IReadOnlyList<WorkspaceLoadError> LoadErrors { get; }

    // TODO(layering): move the originals to IWorkspaceDocumentStore once the metamodel-layering branch lands.
    internal XDocument? SchemaDocument { get; set; }
    internal XDocument? DataDocument { get; set; }
}
