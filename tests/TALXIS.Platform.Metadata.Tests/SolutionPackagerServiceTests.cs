using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Packaging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;
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

            var packResult = service.Pack(inputPath, zipPath, managed: false);

            Assert.True(File.Exists(zipPath));
            Assert.False(packResult.HasErrors);
            Assert.False(packResult.HasMissingRootComponents);

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
    public void Pack_WithRootComponentMissingFiles_ReportsMissingRootComponents()
    {
        var service = new SolutionPackagerService();
        var root = Path.Combine(Path.GetTempPath(), $"packager-missing-root-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(root, "input");
        var zipPath = Path.Combine(root, "packed", "solution.zip");
        const string missingStepId = "35c5f395-d4b2-45b5-8c26-1d250f6d0b72";

        try
        {
            CopyDirectory(SamplePath, inputPath);
            EnsureSolutionPackagerRequiredAttributes(inputPath);
            AddRootComponent(inputPath, type: "92", id: $"{{{missingStepId}}}");

            var result = service.Pack(inputPath, zipPath, managed: false);

            Assert.True(File.Exists(zipPath));
            Assert.False(result.HasErrors);
            Assert.True(result.HasMissingRootComponents);
            var warning = Assert.Single(result.MissingRootComponentWarnings);
            Assert.Contains(missingStepId, warning);
            Assert.Contains(warning, result.Warnings);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Pack_WithEntityFormRootComponents_ReportsOnlyFormsNotPacked()
    {
        var service = new SolutionPackagerService();
        var root = Path.Combine(Path.GetTempPath(), $"packager-forms-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(root, "input");
        var zipPath = Path.Combine(root, "packed", "solution.zip");
        var packedFormId = Guid.NewGuid();
        var ghostFormId = Guid.NewGuid();

        var solution = new Solution
        {
            UniqueName = "FormSolution",
            DisplayName = new Label("Form Solution"),
            Publisher = new Publisher { UniqueName = "test", Prefix = "test" }
        };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "test_contact" });
        solution.AddRootComponent(new RootComponent { Type = ComponentType.SystemForm, Id = packedFormId });
        solution.AddRootComponent(new RootComponent { Type = ComponentType.SystemForm, Id = ghostFormId });
        var workspace = new Workspace("in-memory");
        workspace.AddSolution(solution);
        workspace.AddEntity(new EntityMetadata
        {
            LogicalName = "test_contact",
            SchemaName = "test_contact",
            DisplayName = new Label("Test Contact"),
            PluralName = new Label("Test Contacts"),
            EntitySetName = "test_contacts",
            Ownership = OwnershipType.UserOwned,
            IsCustomEntity = true
        });

        try
        {
            new XmlWorkspaceWriter().Write(workspace, inputPath);
            // The writer emits neither Customizations.xml nor forms without an original document.
            File.Copy(Path.Combine(SamplePath, "Other", "Customizations.xml"), Path.Combine(inputPath, "Other", "Customizations.xml"));
            var formFolder = Path.Combine(inputPath, "Entities", "test_contact", "FormXml", "main");
            Directory.CreateDirectory(formFolder);
            File.WriteAllText(
                Path.Combine(formFolder, $"{packedFormId:B}.xml"),
                $"<forms><systemform><formid>{packedFormId:B}</formid><form /></systemform></forms>");

            var result = service.Pack(inputPath, zipPath, managed: false);

            Assert.False(result.HasErrors);
            Assert.Equal(new MissingRootComponent("SystemForm", ghostFormId.ToString("B")), Assert.Single(result.MissingRootComponents));
            Assert.Contains(Assert.Single(result.MissingRootComponentWarnings), result.Warnings);
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

    private static void AddRootComponent(string workspacePath, string type, string id)
    {
        var solutionXmlPath = Path.Combine(workspacePath, "Other", "Solution.xml");
        var document = XDocument.Load(solutionXmlPath);
        var rootComponents = document.Descendants("RootComponents").Single();
        rootComponents.Add(new XElement("RootComponent",
            new XAttribute("type", type),
            new XAttribute("id", id),
            new XAttribute("behavior", "0")));
        document.Save(solutionXmlPath);
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
