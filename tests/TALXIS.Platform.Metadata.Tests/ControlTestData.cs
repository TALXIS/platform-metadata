using System.IO.Compression;

namespace TALXIS.Platform.Metadata.Tests;

/// <summary>
/// Shared fixtures for custom-control reader tests: sample manifest/solution XML and zip builders.
/// </summary>
internal static class ControlTestData
{
    public const string GridManifestXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <manifest>
          <control namespace="TALXIS.PCF" constructor="Grid" version="0.0.59648" display-name-key="TALXIS Grid" control-type="standard" api-version="1.3.18">
            <external-service-usage enabled="false"/>
            <data-set name="Grid" display-name-key="Main Dataset"/>
            <data-set name="RibbonGroupingDataset" display-name-key="Ribbon Grouping Dataset"/>
            <property name="Columns" display-name-key="Columns" default-value="[]" of-type="Multiple" usage="input" required="false"/>
            <property name="RowHeight" display-name-key="Row Height" of-type="Whole.None" default-value="42" usage="input" required="false"/>
            <property name="EnableEditing" display-name-key="Enable Editing" of-type="Enum" usage="input">
              <value name="Yes" display-name-key="Yes">true</value>
              <value name="No" default="true" display-name-key="No">false</value>
            </property>
            <property name="ClientApiWebresourceName" display-name-key="Client API Webresource Name" of-type="SingleLine.Text" usage="input"/>
          </control>
        </manifest>
        """;

    public const string MapManifestXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <manifest>
          <control namespace="TALXIS.PCF" constructor="Map" version="1.0.0" control-type="standard">
            <data-set name="Locations"/>
          </control>
        </manifest>
        """;

    public const string CustomizationsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml>
          <CustomControls>
            <CustomControl>
              <Name>talxis_TALXIS.PCF.Grid</Name>
            </CustomControl>
          </CustomControls>
        </ImportExportXml>
        """;

    public const string SolutionXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml version="9.2.0.0">
          <SolutionManifest>
            <UniqueName>TALXIS.PCF.Grid.Solution</UniqueName>
            <Version>1.0.0.0</Version>
            <Managed>1</Managed>
            <Publisher>
              <UniqueName>talxis</UniqueName>
              <CustomizationPrefix>talxis</CustomizationPrefix>
            </Publisher>
          </SolutionManifest>
        </ImportExportXml>
        """;

    public static byte[] BuildZip(params (string EntryName, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entryStream = archive.CreateEntry(name).Open();
                entryStream.Write(content);
            }
        }
        return stream.ToArray();
    }
}
