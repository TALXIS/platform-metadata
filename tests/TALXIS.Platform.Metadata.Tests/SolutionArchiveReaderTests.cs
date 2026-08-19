using System.Text;
using TALXIS.Platform.Metadata.Serialization.Xml.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionArchiveReaderTests
{
    [Fact]
    public void Read_SolutionZip_PopulatesManifestPublisherAndControls()
    {
        var zip = ControlTestData.BuildZip(
            ("solution.xml", Encoding.UTF8.GetBytes(ControlTestData.SolutionXml)),
            ("customizations.xml", Encoding.UTF8.GetBytes(ControlTestData.CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)),
            ("Controls/talxis_TALXIS.PCF.Map/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.MapManifestXml)));

        using var stream = new MemoryStream(zip);
        var solution = SolutionArchiveReader.Read(stream);

        Assert.Equal("TALXIS.PCF.Grid.Solution", solution.UniqueName);
        Assert.True(solution.IsManaged);
        Assert.Equal("talxis", solution.Publisher?.Prefix);

        Assert.Equal(2, solution.Controls.Count);
        var grid = solution.Controls.Single(c => c.Manifest.Constructor == "Grid");
        Assert.Equal("talxis_TALXIS.PCF.Grid", grid.Name);
        var map = solution.Controls.Single(c => c.Manifest.Constructor == "Map");
        Assert.Equal("talxis_TALXIS.PCF.Map", map.Name);
    }

    [Fact]
    public void Read_SolutionZipWithoutSolutionXml_StillReadsControls()
    {
        var zip = ControlTestData.BuildZip(
            ("customizations.xml", Encoding.UTF8.GetBytes(ControlTestData.CustomizationsXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)));

        using var stream = new MemoryStream(zip);
        var solution = SolutionArchiveReader.Read(stream);

        Assert.Equal("Unknown", solution.UniqueName);
        Assert.Null(solution.Publisher);
        var control = Assert.Single(solution.Controls);
        Assert.Equal("talxis_TALXIS.PCF.Grid", control.Name);
    }

    [Fact]
    public void ReadPackage_NupkgWithSolution_ReturnsSolution()
    {
        var solutionZip = ControlTestData.BuildZip(
            ("solution.xml", Encoding.UTF8.GetBytes(ControlTestData.SolutionXml)),
            ("Controls/talxis_TALXIS.PCF.Grid/ControlManifest.xml", Encoding.UTF8.GetBytes(ControlTestData.GridManifestXml)));
        var pdpkgZip = ControlTestData.BuildZip(("PkgAssets/Grid.Solution.zip", solutionZip));
        var nupkg = ControlTestData.BuildZip(("contentFiles/any/any/Grid.pdpkg.zip", pdpkgZip));

        using var stream = new MemoryStream(nupkg);
        var solutions = SolutionPackageReader.Read(stream);

        var solution = Assert.Single(solutions);
        Assert.Equal("TALXIS.PCF.Grid.Solution", solution.UniqueName);
        var control = Assert.Single(solution.Controls);
        Assert.Equal("talxis_TALXIS.PCF.Grid", control.Name);
    }
}
