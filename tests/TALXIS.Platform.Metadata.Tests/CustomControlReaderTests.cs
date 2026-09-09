using System.Text;
using TALXIS.Platform.Metadata.Serialization.Xml.Controls;

namespace TALXIS.Platform.Metadata.Tests;

public class CustomControlReaderTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("metadata-manifest-tests").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Read_BareManifestFile_ParsesControlAndProperties()
    {
        var path = WriteFile("ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml));

        var control = CustomControlReader.Read(path);
        var manifest = control.Manifest;

        Assert.Equal("TALXIS.PCF", manifest.Namespace);
        Assert.Equal("Grid", manifest.Constructor);
        Assert.Equal("TALXIS.PCF.Grid", manifest.QualifiedName);
        Assert.Equal("0.0.59648", manifest.Version);
        Assert.Equal(new[] { "Grid", "RibbonGroupingDataset" }, manifest.DataSets);
        Assert.Null(control.Name);

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
        var zip = ControlTestData.BuildZip(
            ("customizations.xml", Encoding.UTF8.GetBytes(ControlTestData.CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)));
        var path = WriteFile("Grid.Solution.zip", zip);

        var control = CustomControlReader.Read(path);

        Assert.Equal("TALXIS.PCF.Grid", control.Manifest.QualifiedName);
        Assert.Equal("talxis_TALXIS.PCF.Grid", control.Name);
    }

    [Fact]
    public void Read_NestedArchives_FindsManifestThroughPdpkgAndSolution()
    {
        var solutionZip = ControlTestData.BuildZip(
            ("customizations.xml", Encoding.UTF8.GetBytes(ControlTestData.CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)));
        var pdpkgZip = ControlTestData.BuildZip(("PkgAssets/Grid.Solution.zip", solutionZip));
        var nupkg = ControlTestData.BuildZip(("contentFiles/any/any/Grid.pdpkg.zip", pdpkgZip));
        var path = WriteFile("grid.nupkg", nupkg);

        var control = CustomControlReader.Read(path);

        Assert.Equal("TALXIS.PCF.Grid", control.Manifest.QualifiedName);
        Assert.Equal("talxis_TALXIS.PCF.Grid", control.Name);
    }

    [Fact]
    public void Read_SolutionZipWithoutCustomizations_FallsBackToControlsFolderName()
    {
        var zip = ControlTestData.BuildZip(
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)));
        var path = WriteFile("NoCustomizations.zip", zip);

        var control = CustomControlReader.Read(path);

        Assert.Equal("talxis_TALXIS.PCF.Grid", control.Name);
    }

    [Fact]
    public void Read_SolutionZipWithMultipleControls_Throws()
    {
        var zip = ControlTestData.BuildZip(
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)),
            ("Controls/talxis_TALXIS.PCF.Map/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.MapManifestXml)));
        var path = WriteFile("TwoControls.zip", zip);

        var ex = Assert.Throws<InvalidOperationException>(() => CustomControlReader.Read(path));
        Assert.Contains("Multiple custom controls", ex.Message);
        Assert.Contains("talxis_TALXIS.PCF.Grid", ex.Message);
        Assert.Contains("talxis_TALXIS.PCF.Map", ex.Message);
    }

    [Fact]
    public void Read_ZipWithoutManifest_Throws()
    {
        var zip = ControlTestData.BuildZip(("readme.txt", Encoding.UTF8.GetBytes("nothing here")));
        var path = WriteFile("empty.zip", zip);

        Assert.Throws<InvalidOperationException>(() => CustomControlReader.Read(path));
    }

    [Fact]
    public void Read_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => CustomControlReader.Read(Path.Combine(_tempDir, "missing.xml")));
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
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", ControlTestData.GridManifestXml);
        WriteProjectFile(@"proj\out\controls\Grid\ControlManifest.xml", ControlTestData.GridManifestXml);
        WriteProjectFile(@"proj\node_modules\pkg\ControlManifest.xml", ControlTestData.GridManifestXml);

        var control = CustomControlReader.Read(Path.Combine(_tempDir, "proj"));

        Assert.Equal("TALXIS.PCF.Grid", control.Manifest.QualifiedName);
    }

    [Fact]
    public void Read_CsprojPath_FindsManifestInProjectFolder()
    {
        var csproj = WriteProjectFile(@"proj\Grid.pcfproj.csproj", "<Project />");
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", ControlTestData.GridManifestXml);

        var control = CustomControlReader.Read(csproj);

        Assert.Equal("TALXIS.PCF.Grid", control.Manifest.QualifiedName);
    }

    [Fact]
    public void Read_ProjectFolderWithMultipleManifests_Throws()
    {
        WriteProjectFile(@"proj\Grid\ControlManifest.Input.xml", ControlTestData.GridManifestXml);
        WriteProjectFile(@"proj\Map\ControlManifest.Input.xml", ControlTestData.GridManifestXml);

        var ex = Assert.Throws<InvalidOperationException>(() => CustomControlReader.Read(Path.Combine(_tempDir, "proj")));
        Assert.Contains("Multiple control manifests", ex.Message);
    }

    [Fact]
    public void Read_ProjectFolderWithoutManifest_Throws()
    {
        WriteProjectFile(@"proj\readme.md", "nothing");

        Assert.Throws<InvalidOperationException>(() => CustomControlReader.Read(Path.Combine(_tempDir, "proj")));
    }
}
