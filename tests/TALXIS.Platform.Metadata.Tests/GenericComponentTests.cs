using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class GenericComponentTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void Load_PicksUpGenericFilesInOther()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        // ConnectionRoles.xml should be loaded as generic (not Solution.xml or Relationships.xml)
        var connRoles = workspace.GenericComponents.FirstOrDefault(
            c => c.FilePath == Path.Combine("Other", "ConnectionRoles.xml"));
        Assert.NotNull(connRoles);
        Assert.Equal("ConnectionRoles", connRoles.ComponentTypeName);
        Assert.NotNull(connRoles.SerializedContent);
        Assert.Contains("Stakeholder", connRoles.SerializedContent);
    }

    [Fact]
    public void Load_PicksUpGenericFromUncoveredDirectory()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var envVar = workspace.GenericComponents.FirstOrDefault(
            c => c.FilePath != null && c.FilePath.Contains("tp_SomeVar.xml"));
        Assert.NotNull(envVar);
        Assert.Equal("environmentvariabledefinition", envVar.ComponentTypeName);
        Assert.Equal("tp_SomeVar", envVar.Name);
    }

    [Fact]
    public void GenericComponents_SurviveRoundtrip()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputDir = Path.Combine(Path.GetTempPath(), "GenericRoundtripTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputDir);

            // Verify connection roles file was written
            var connRolesPath = Path.Combine(outputDir, "Other", "ConnectionRoles.xml");
            Assert.True(File.Exists(connRolesPath), "ConnectionRoles.xml should be written");
            var content = File.ReadAllText(connRolesPath);
            Assert.Contains("Stakeholder", content);

            // Verify env var file was written
            var envVarPath = Path.Combine(outputDir, "EnvironmentVariableDefinitions", "tp_SomeVar.xml");
            Assert.True(File.Exists(envVarPath), "tp_SomeVar.xml should be written");
            var envContent = File.ReadAllText(envVarPath);
            Assert.Contains("tp_SomeVar", envContent);

            // Re-read and verify
            var workspace2 = reader.Load(outputDir);
            Assert.True(workspace2.GenericComponents.Count >= 2);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void DedicatedLoaders_NotDuplicatedAsGeneric()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        // Solution.xml and Relationships.xml in Other/ should NOT appear as generic
        Assert.DoesNotContain(workspace.GenericComponents,
            c => c.FilePath == Path.Combine("Other", "Solution.xml"));
        Assert.DoesNotContain(workspace.GenericComponents,
            c => c.FilePath == Path.Combine("Other", "Relationships.xml"));

        // Entities directory is dedicated -- should not appear as generic
        Assert.DoesNotContain(workspace.GenericComponents,
            c => c.FilePath != null && c.FilePath.StartsWith("Entities"));
    }
}
