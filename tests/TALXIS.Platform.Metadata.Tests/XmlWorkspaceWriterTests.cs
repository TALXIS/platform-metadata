using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Solutions;
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

    [Fact]
    public void Write_InMemoryEntity_ProducesValidXml()
    {
        var workspace = new Workspace("in-memory");
        workspace.Solution = new Solution
        {
            UniqueName = "InMemorySolution",
            Version = "2.0",
            Publisher = new Publisher { UniqueName = "test", Prefix = "test" }
        };

        var entity = new EntityMetadata
        {
            LogicalName = "test_contact",
            SchemaName = "test_contact",
            DisplayName = new Label("Test Contact"),
            PluralName = new Label("Test Contacts"),
            EntitySetName = "test_contacts",
            Ownership = OwnershipType.UserOwned,
            IsCustomEntity = true
        };
        entity.AddAttribute(new StringAttributeMetadata
        {
            LogicalName = "test_fullname",
            SchemaName = "test_fullname",
            DisplayName = new Label("Full Name"),
            IsCustomAttribute = true
        });
        workspace.AddEntity(entity);

        var outputPath = Path.Combine(Path.GetTempPath(), $"writer-inmemory-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            Assert.True(File.Exists(Path.Combine(outputPath, "Other", "Solution.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "test_contact", "Entity.xml")));

            // Verify it's valid XML that can be re-loaded
            var reader = new XmlWorkspaceReader();
            var reloaded = reader.Load(outputPath);
            var reloadedEntity = reloaded.FindEntity("test_contact");
            Assert.NotNull(reloadedEntity);
            Assert.Equal("Test Contact", reloadedEntity.DisplayName.Default);
            Assert.Single(reloadedEntity.Attributes);
            Assert.Equal("test_fullname", reloadedEntity.Attributes[0].LogicalName);
        }
        finally
        {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Write_SolutionWithNoEntities_OnlySolutionXmlCreated()
    {
        var workspace = new Workspace("empty");
        workspace.Solution = new Solution
        {
            UniqueName = "EmptySolution",
            Version = "1.0",
            Publisher = new Publisher { UniqueName = "test", Prefix = "test" }
        };

        var outputPath = Path.Combine(Path.GetTempPath(), $"writer-noentities-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            Assert.True(File.Exists(Path.Combine(outputPath, "Other", "Solution.xml")));
            Assert.False(Directory.Exists(Path.Combine(outputPath, "Entities")));
        }
        finally
        {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Write_MultipleEntities_OneFolderPerEntity()
    {
        var workspace = new Workspace("multi");
        workspace.Solution = new Solution
        {
            UniqueName = "MultiEntitySolution",
            Version = "1.0",
            Publisher = new Publisher { UniqueName = "test", Prefix = "test" }
        };

        workspace.AddEntity(new EntityMetadata
        {
            LogicalName = "test_alpha",
            DisplayName = new Label("Alpha"),
            PluralName = new Label("Alphas"),
            EntitySetName = "test_alphas"
        });
        workspace.AddEntity(new EntityMetadata
        {
            LogicalName = "test_beta",
            DisplayName = new Label("Beta"),
            PluralName = new Label("Betas"),
            EntitySetName = "test_betas"
        });
        workspace.AddEntity(new EntityMetadata
        {
            LogicalName = "test_gamma",
            DisplayName = new Label("Gamma"),
            PluralName = new Label("Gammas"),
            EntitySetName = "test_gammas"
        });

        var outputPath = Path.Combine(Path.GetTempPath(), $"writer-multi-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var entityDirs = Directory.GetDirectories(Path.Combine(outputPath, "Entities"));
            Assert.Equal(3, entityDirs.Length);
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "test_alpha", "Entity.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "test_beta", "Entity.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "test_gamma", "Entity.xml")));
        }
        finally
        {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_LoadWriteReload_EntityAndAttributeCountsMatch()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var original = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-full-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(original, outputPath);

            var reloaded = reader.Load(outputPath);

            Assert.Equal(original.Solution!.UniqueName, reloaded.Solution!.UniqueName);
            Assert.Equal(original.Entities.Count, reloaded.Entities.Count);
            Assert.Equal(original.GlobalOptionSets.Count, reloaded.GlobalOptionSets.Count);
            Assert.Equal(original.Relationships.Count, reloaded.Relationships.Count);

            foreach (var origEntity in original.Entities)
            {
                var reloadedEntity = reloaded.FindEntity(origEntity.LogicalName);
                Assert.NotNull(reloadedEntity);
                Assert.Equal(origEntity.Attributes.Count, reloadedEntity.Attributes.Count);
                Assert.Equal(origEntity.DisplayName.Default, reloadedEntity.DisplayName.Default);
                Assert.Equal(origEntity.EntitySetName, reloadedEntity.EntitySetName);
            }
        }
        finally
        {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void RoundtripPreservesUnknownElements()
    {
        // Create an Entity.xml with extra unknown elements
        var dir = Path.Combine(Path.GetTempPath(), $"roundtrip-unknown-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));

            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml version="9.1">
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames><LocalizedName description="Test Entity" languagecode="1033" /></LocalizedNames>
                      <LocalizedCollectionNames><LocalizedCollectionName description="Test Entities" languagecode="1033" /></LocalizedCollectionNames>
                      <OwnershipTypeMask>UserOwned</OwnershipTypeMask>
                      <IsCustomEntity>1</IsCustomEntity>
                      <CustomUnknownElement>preserve-this-value</CustomUnknownElement>
                      <AnotherUnknown flag="true">data</AnotherUnknown>
                      <attributes>
                        <attribute PhysicalName="test_name">
                          <Type>nvarchar</Type>
                          <LogicalName>test_name</LogicalName>
                          <MaxLength>100</MaxLength>
                          <IsCustomField>1</IsCustomField>
                          <displaynames><displayname description="Name" languagecode="1033" /></displaynames>
                          <MyCustomAttrElement>keep-me</MyCustomAttrElement>
                        </attribute>
                      </attributes>
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            // Load → Write → verify unknown elements are preserved
            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(dir);

            var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-unknown-out-{Guid.NewGuid():N}");
            try
            {
                var writer = new XmlWorkspaceWriter();
                writer.Write(workspace, outputPath);

                var writtenDoc = System.Xml.Linq.XDocument.Load(
                    Path.Combine(outputPath, "Entities", "test_entity", "Entity.xml"));
                var writtenEntity = writtenDoc.Root!.Element("EntityInfo")!.Element("entity")!;

                // Unknown entity-level elements preserved
                Assert.Equal("preserve-this-value",
                    writtenEntity.Element("CustomUnknownElement")?.Value);
                var anotherUnknown = writtenEntity.Element("AnotherUnknown");
                Assert.NotNull(anotherUnknown);
                Assert.Equal("data", anotherUnknown.Value);
                Assert.Equal("true", anotherUnknown.Attribute("flag")?.Value);

                // Unknown attribute-level elements preserved
                var writtenAttr = writtenEntity.Element("attributes")!.Elements("attribute").First();
                Assert.Equal("keep-me", writtenAttr.Element("MyCustomAttrElement")?.Value);
            }
            finally
            {
                if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
            }
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
