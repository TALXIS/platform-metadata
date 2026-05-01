using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlWorkspaceWriterTests
{
    private const string SamplePath = "/tmp/dpp-sample/sample-repo/src/Solutions.DataModel";

    private static bool SampleRepoExists() => Directory.Exists(SamplePath);

    [Fact]
    public void RoundtripSampleRepo()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            // Verify key files exist
            Assert.True(File.Exists(Path.Combine(outputPath, "Other", "Solution.xml")));
            Assert.True(Directory.Exists(Path.Combine(outputPath, "Entities")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "udpp_warehouseitem", "Entity.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "udpp_warehousetransaction", "Entity.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "OptionSets", "udpp_paymentmethod.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Other", "Relationships.xml")));
        }
        finally
        {
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void RoundtripSampleRepo_SolutionXmlPreservesStructure()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var originalDoc = XDocument.Load(Path.Combine(SamplePath, "Other", "Solution.xml"));
            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "Other", "Solution.xml"));

            var origManifest = originalDoc.Root!.Element("SolutionManifest")!;
            var writtenManifest = writtenDoc.Root!.Element("SolutionManifest")!;

            // Core values preserved
            Assert.Equal(origManifest.Element("UniqueName")!.Value, writtenManifest.Element("UniqueName")!.Value);
            Assert.Equal(origManifest.Element("Version")!.Value, writtenManifest.Element("Version")!.Value);

            // Publisher preserved
            var origPub = origManifest.Element("Publisher")!;
            var writtenPub = writtenManifest.Element("Publisher")!;
            Assert.Equal(origPub.Element("UniqueName")!.Value, writtenPub.Element("UniqueName")!.Value);
            Assert.Equal(origPub.Element("CustomizationPrefix")!.Value, writtenPub.Element("CustomizationPrefix")!.Value);
            Assert.Equal(origPub.Element("CustomizationOptionValuePrefix")!.Value, writtenPub.Element("CustomizationOptionValuePrefix")!.Value);

            // Unknown elements preserved (EMailAddress, SupportingWebsiteUrl, Addresses, etc.)
            Assert.NotNull(writtenPub.Element("EMailAddress"));
            Assert.NotNull(writtenPub.Element("Addresses"));
            Assert.NotNull(writtenPub.Element("SupportingWebsiteUrl"));

            // Root components preserved
            var origComponents = origManifest.Element("RootComponents")!.Elements("RootComponent").ToList();
            var writtenComponents = writtenManifest.Element("RootComponents")!.Elements("RootComponent").ToList();
            Assert.Equal(origComponents.Count, writtenComponents.Count);
        }
        finally
        {
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void RoundtripSampleRepo_EntityXmlPreservesUnknownElements()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var originalDoc = XDocument.Load(Path.Combine(SamplePath, "Entities", "udpp_warehouseitem", "Entity.xml"));
            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "Entities", "udpp_warehouseitem", "Entity.xml"));

            var origEntity = originalDoc.Root!.Element("EntityInfo")!.Element("entity")!;
            var writtenEntity = writtenDoc.Root!.Element("EntityInfo")!.Element("entity")!;

            // Known values preserved
            Assert.Equal(origEntity.Attribute("Name")!.Value, writtenEntity.Attribute("Name")!.Value);
            Assert.Equal(origEntity.Element("EntitySetName")!.Value, writtenEntity.Element("EntitySetName")!.Value);
            Assert.Equal(origEntity.Element("OwnershipTypeMask")!.Value, writtenEntity.Element("OwnershipTypeMask")!.Value);

            // Unknown elements preserved (e.g. IsDuplicateCheckSupported, IsCollaboration)
            Assert.NotNull(writtenEntity.Element("IsDuplicateCheckSupported"));
            Assert.NotNull(writtenEntity.Element("IsCollaboration"));
            Assert.NotNull(writtenEntity.Element("IntroducedVersion"));

            // Attribute count preserved
            var origAttrs = origEntity.Element("attributes")!.Elements("attribute").Count();
            var writtenAttrs = writtenEntity.Element("attributes")!.Elements("attribute").Count();
            Assert.Equal(origAttrs, writtenAttrs);

            // Unknown attribute child elements preserved
            var origFirstAttr = origEntity.Element("attributes")!.Elements("attribute").First();
            var writtenFirstAttr = writtenEntity.Element("attributes")!.Elements("attribute").First();
            Assert.NotNull(writtenFirstAttr.Element("ImeMode"));
            Assert.NotNull(writtenFirstAttr.Element("ValidForUpdateApi"));
            Assert.NotNull(writtenFirstAttr.Element("SourceType"));
        }
        finally
        {
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void RoundtripSampleRepo_OptionSetXmlPreservesStructure()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var originalDoc = XDocument.Load(Path.Combine(SamplePath, "OptionSets", "udpp_paymentmethod.xml"));
            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "OptionSets", "udpp_paymentmethod.xml"));

            // Root attributes preserved
            Assert.Equal(originalDoc.Root!.Attribute("Name")!.Value, writtenDoc.Root!.Attribute("Name")!.Value);
            Assert.Equal(originalDoc.Root!.Attribute("localizedName")!.Value, writtenDoc.Root!.Attribute("localizedName")!.Value);

            // Unknown elements preserved
            Assert.NotNull(writtenDoc.Root!.Element("OptionSetType"));
            Assert.Equal("picklist", writtenDoc.Root!.Element("OptionSetType")!.Value);
            Assert.NotNull(writtenDoc.Root!.Element("IntroducedVersion"));
            Assert.NotNull(writtenDoc.Root!.Element("ExternalTypeName"));

            // Options preserved
            var origOptions = originalDoc.Root!.Element("options")!.Elements("option").ToList();
            var writtenOptions = writtenDoc.Root!.Element("options")!.Elements("option").ToList();
            Assert.Equal(origOptions.Count, writtenOptions.Count);
            Assert.Equal(origOptions[0].Attribute("value")!.Value, writtenOptions[0].Attribute("value")!.Value);

            // Option ExternalValue and IsHidden preserved
            Assert.NotNull(writtenOptions[0].Attribute("ExternalValue"));
            Assert.NotNull(writtenOptions[0].Attribute("IsHidden"));
        }
        finally
        {
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void RoundtripSampleRepo_LoadWrittenOutput()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            // Re-read the written output and verify it loads correctly
            var workspace2 = reader.Load(outputPath);

            Assert.NotNull(workspace2.Solution);
            Assert.Equal(workspace.Solution!.UniqueName, workspace2.Solution.UniqueName);
            Assert.Equal(workspace.Solution.Version, workspace2.Solution.Version);
            Assert.Equal(workspace.Entities.Count, workspace2.Entities.Count);
            Assert.Equal(workspace.GlobalOptionSets.Count, workspace2.GlobalOptionSets.Count);
            Assert.Equal(workspace.Relationships.Count, workspace2.Relationships.Count);

            // Entity details
            var entity1 = workspace.FindEntity("udpp_warehouseitem")!;
            var entity2 = workspace2.FindEntity("udpp_warehouseitem")!;
            Assert.Equal(entity1.DisplayName.Default, entity2.DisplayName.Default);
            Assert.Equal(entity1.PluralName.Default, entity2.PluralName.Default);
            Assert.Equal(entity1.EntitySetName, entity2.EntitySetName);
            Assert.Equal(entity1.Attributes.Count, entity2.Attributes.Count);
        }
        finally
        {
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }
    }
}
