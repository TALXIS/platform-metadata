using TALXIS.Platform.Metadata.Packaging;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionPackagerServiceTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void PackAndUnpack_SampleWorkspace_RoundTrips()
    {
        var service = new SolutionPackagerService();
        var root = Path.Combine(Path.GetTempPath(), $"packager-roundtrip-{Guid.NewGuid():N}");
        var zipPath = Path.Combine(root, "packed", "solution.zip");
        var unpackPath = Path.Combine(root, "unpacked");

        try
        {
            service.Pack(SamplePath, zipPath, managed: false);

            Assert.True(File.Exists(zipPath));

            service.Unpack(zipPath, unpackPath, managed: false);

            Assert.True(File.Exists(Path.Combine(unpackPath, "Other", "Solution.xml")));
            Assert.True(File.Exists(Path.Combine(unpackPath, "Entities", "test_entity", "Entity.xml")));

            var workspace = new XmlWorkspaceReader().Load(unpackPath);
            var solution = Assert.Single(workspace.Solutions);

            Assert.Equal("test_solution", solution.UniqueName);
            Assert.NotNull(workspace.FindEntity("test_entity"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Pack_WithMissingFolder_ThrowsDirectoryNotFoundException()
    {
        var service = new SolutionPackagerService();
        var missingFolder = Path.Combine(Path.GetTempPath(), $"missing-folder-{Guid.NewGuid():N}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"packager-{Guid.NewGuid():N}", "solution.zip");

        var ex = Assert.Throws<DirectoryNotFoundException>(() => service.Pack(missingFolder, zipPath, managed: false));

        Assert.Contains(missingFolder, ex.Message);
    }

    [Fact]
    public void Unpack_WithMissingZip_ThrowsFileNotFoundException()
    {
        var service = new SolutionPackagerService();
        var zipPath = Path.Combine(Path.GetTempPath(), $"missing-zip-{Guid.NewGuid():N}.zip");
        var outputFolder = Path.Combine(Path.GetTempPath(), $"packager-output-{Guid.NewGuid():N}");

        var ex = Assert.Throws<FileNotFoundException>(() => service.Unpack(zipPath, outputFolder, managed: false));

        Assert.Equal(zipPath, ex.FileName);
    }

    [Fact]
    public void Pack_WithMissingMappingFile_ThrowsFileNotFoundException()
    {
        var service = new SolutionPackagerService();
        var zipPath = Path.Combine(Path.GetTempPath(), $"packager-{Guid.NewGuid():N}", "solution.zip");
        var missingMap = Path.Combine(Path.GetTempPath(), $"missing-map-{Guid.NewGuid():N}.xml");

        var ex = Assert.Throws<FileNotFoundException>(() => service.Pack(
            SamplePath,
            zipPath,
            new SolutionPackagerOptions
            {
                Managed = false,
                MappingFilePath = missingMap
            }));

        Assert.Equal(missingMap, ex.FileName);
    }

    [Fact]
    public void Unpack_WithNullOptions_ThrowsArgumentNullException()
    {
        var service = new SolutionPackagerService();
        var zipPath = Path.Combine(Path.GetTempPath(), $"missing-zip-{Guid.NewGuid():N}.zip");
        var outputFolder = Path.Combine(Path.GetTempPath(), $"packager-output-{Guid.NewGuid():N}");

        Assert.Throws<ArgumentNullException>(() => service.Unpack(zipPath, outputFolder, options: null!));
    }

    [Fact]
    public void Pack_WithBlankZipPath_ThrowsArgumentException()
    {
        var service = new SolutionPackagerService();

        var ex = Assert.Throws<ArgumentException>(() => service.Pack(SamplePath, " ", managed: false));

        Assert.Equal("zipPath", ex.ParamName);
    }
}
