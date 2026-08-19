using System.IO.Compression;
using System.Text;
using TALXIS.Platform.Metadata.Serialization.Xml.Controls;

namespace TALXIS.Platform.Metadata.Tests;

public class ControlManifestReaderTests : IDisposable
{
    private const string GridManifestXml = """
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

    private const string CustomizationsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml>
          <CustomControls>
            <CustomControl>
              <Name>talxis_TALXIS.PCF.Grid</Name>
            </CustomControl>
          </CustomControls>
        </ImportExportXml>
        """;

    private readonly string _tempDir = Directory.CreateTempSubdirectory("metadata-manifest-tests").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] BuildZip(params (string EntryName, byte[] Content)[] entries)
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

    [Fact]
    public void Read_BareManifestFile_ParsesControlAndProperties()
    {
        var path = WriteFile("ControlManifest.xml", Encoding.UTF8.GetBytes(GridManifestXml));

        var manifest = ControlManifestReader.Read(path);

        Assert.Equal("TALXIS.PCF", manifest.Namespace);
        Assert.Equal("Grid", manifest.Constructor);
        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
        Assert.Equal("0.0.59648", manifest.Version);
        Assert.Equal(new[] { "Grid", "RibbonGroupingDataset" }, manifest.DataSets);
        Assert.Null(manifest.PrefixedName);

        var enableEditing = manifest.Properties.Single(p => p.Name == "EnableEditing");
        Assert.Equal("Enum", enableEditing.OfType);
        Assert.Equal(new[] { "true", "false" }, enableEditing.EnumValues);

        var rowHeight = manifest.Properties.Single(p => p.Name == "RowHeight");
        Assert.Equal("Whole.None", rowHeight.OfType);
        Assert.Equal("42", rowHeight.DefaultValue);
    }

    [Fact]
    public void Read_SolutionZip_ResolvesPrefixedNameFromCustomizations()
    {
        var zip = BuildZip(
            ("customizations.xml", Encoding.UTF8.GetBytes(CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(GridManifestXml)));
        var path = WriteFile("Grid.Solution.zip", zip);

        var manifest = ControlManifestReader.Read(path);

        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
        Assert.Equal("talxis_TALXIS.PCF.Grid", manifest.PrefixedName);
    }

    [Fact]
    public void Read_NestedArchives_FindsManifestThroughPdpkgAndSolution()
    {
        var solutionZip = BuildZip(
            ("customizations.xml", Encoding.UTF8.GetBytes(CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(GridManifestXml)));
        var pdpkgZip = BuildZip(("PkgAssets/Grid.Solution.zip", solutionZip));
        var nupkg = BuildZip(("contentFiles/any/any/Grid.pdpkg.zip", pdpkgZip));
        var path = WriteFile("grid.nupkg", nupkg);

        var manifest = ControlManifestReader.Read(path);

        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
        Assert.Equal("talxis_TALXIS.PCF.Grid", manifest.PrefixedName);
    }

    [Fact]
    public void Read_SolutionZipWithoutCustomizations_FallsBackToControlsFolderName()
    {
        var zip = BuildZip(("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(GridManifestXml)));
        var path = WriteFile("NoCustomizations.zip", zip);

        var manifest = ControlManifestReader.Read(path);

        Assert.Equal("talxis_TALXIS.PCF.Grid", manifest.PrefixedName);
    }

    [Fact]
    public void Read_ZipWithoutManifest_Throws()
    {
        var zip = BuildZip(("readme.txt", Encoding.UTF8.GetBytes("nothing here")));
        var path = WriteFile("empty.zip", zip);

        Assert.Throws<InvalidOperationException>(() => ControlManifestReader.Read(path));
    }

    [Fact]
    public void Read_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ControlManifestReader.Read(Path.Combine(_tempDir, "missing.xml")));
    }

    private string WriteProjectFile(string relativePath, string content)
    {
        var path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Read_ProjectFolder_FindsSourceManifestIgnoringBuildOutput()
    {
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", GridManifestXml);
        WriteProjectFile(@"proj\out\controls\Grid\ControlManifest.xml", GridManifestXml);
        WriteProjectFile(@"proj\node_modules\pkg\ControlManifest.xml", GridManifestXml);

        var manifest = ControlManifestReader.Read(Path.Combine(_tempDir, "proj"));

        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
    }

    [Fact]
    public void Read_CsprojPath_FindsManifestInProjectFolder()
    {
        var csproj = WriteProjectFile(@"proj\Grid.pcfproj.csproj", "<Project />");
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", GridManifestXml);

        var manifest = ControlManifestReader.Read(csproj);

        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
    }

    [Fact]
    public void Read_ProjectFolderWithMultipleManifests_Throws()
    {
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", GridManifestXml);
        WriteProjectFile(@"proj\Map\ControlManifest.Input.xml", GridManifestXml);

        var ex = Assert.Throws<InvalidOperationException>(() => ControlManifestReader.Read(Path.Combine(_tempDir, "proj")));
        Assert.Contains("Multiple control manifests", ex.Message);
    }

    [Fact]
    public void Read_ProjectFolderWithoutManifest_Throws()
    {
        WriteProjectFile(@"proj\readme.md", "nothing");

        Assert.Throws<InvalidOperationException>(() => ControlManifestReader.Read(Path.Combine(_tempDir, "proj")));
    }
}
