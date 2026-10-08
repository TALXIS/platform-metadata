using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// Builds CMT schema entities from a loaded <see cref="Workspace"/>, so callers can name a table instead of passing its metadata.
/// </summary>
public static class CmtWorkspaceSchemaBuilder
{
    /// <summary>
    /// Runs <see cref="CmtSchemaBuilder.BuildEntity"/> for the table the workspace declares under <paramref name="entityLogicalName"/>.
    /// Throws when the workspace does not declare it; callers decide how to report a coverage gap.
    /// </summary>
    public static CmtSchemaEntity BuildEntity(Workspace metadata, string entityLogicalName, CmtSchemaBuildOptions options, ICollection<string> warnings, CmtDataSchema? target = null)
    {
        var entity = metadata.FindEntity(entityLogicalName)
            ?? throw new InvalidOperationException($"The workspace does not declare entity '{entityLogicalName}'.");

        return CmtSchemaBuilder.BuildEntity(entity, metadata.FindRelationshipsForEntity(entityLogicalName), options, warnings, target, metadata.FindEntity);
    }
}
