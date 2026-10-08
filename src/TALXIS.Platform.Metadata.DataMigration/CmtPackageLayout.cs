namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// File names a Configuration Migration Tool package consists of. CMT requires <see cref="SchemaFileName"/>
/// and <see cref="DataFileName"/> at the root of the package folder (and of the zip); filedata payloads live
/// in <see cref="FilesDirectory"/>. These are CMT's fixed names, not a discovery convention: locating packages
/// stays with the consumer (build tasks, CLI).
/// </summary>
public static class CmtPackageLayout
{
    /// <summary>The schema file, <c>data_schema.xml</c>.</summary>
    public const string SchemaFileName = "data_schema.xml";

    /// <summary>The records file, <c>data.xml</c>.</summary>
    public const string DataFileName = "data.xml";

    /// <summary>The folder holding filedata payloads as <c>&lt;file id&gt;.bin</c>.</summary>
    public const string FilesDirectory = "files";
}
