using TALXIS.Platform.Metadata.Packaging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionPackagerServiceTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void PackAndUnpack_SampleWorkspace_RoundTrips()
    {
        var service = new SolutionPackagerService();
        var root = Path.Combine(Path.GetTempPath(), $"packager-roundtrip-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(root, "input");
        var zipPath = Path.Combine(root, "packed", "solution.zip");
        var unpackPath = Path.Combine(root, "unpacked");

        try
        {
            CopyDirectory(SamplePath, inputPath);
            EnsureSolutionPackagerRequiredAttributes(inputPath);

            service.Pack(inputPath, zipPath, managed: false);

            Assert.True(File.Exists(zipPath));

            service.Unpack(zipPath, unpackPath, managed: false);

            Assert.True(File.Exists(Path.Combine(unpackPath, "Other", "Solution.xml")));

            var workspace = new XmlWorkspaceReader().Load(unpackPath);
            var solution = Assert.Single(workspace.Solutions);

            Assert.Equal("TestSolution", solution.UniqueName);
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

    private static void EnsureSolutionPackagerRequiredAttributes(string workspacePath)
    {
        var solutionXmlPath = Path.Combine(workspacePath, "Other", "Solution.xml");
        var document = XDocument.Load(solutionXmlPath);
        document.Root!.SetAttributeValue("languagecode", "1033");
        document.Root!.SetAttributeValue("generatedBy", "Dataverse");
        document.Descendants("RootComponent")
            .Where(component => (string?)component.Attribute("type") == "1")
            .Remove();
        document.Save(solutionXmlPath);

        var entitiesPath = Path.Combine(workspacePath, "Entities");
        if (Directory.Exists(entitiesPath))
            Directory.Delete(entitiesPath, recursive: true);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(targetDirectory, relativePath));
        }
    }
}
