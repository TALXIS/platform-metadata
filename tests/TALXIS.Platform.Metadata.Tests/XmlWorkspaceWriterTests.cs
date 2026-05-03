using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlWorkspaceWriterTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    
    [Fact]
    public void RoundtripSampleRepo()
    {
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
            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "test_entity", "Entity.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "OptionSets", "tp_teststatus.xml")));
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
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var originalDoc = XDocument.Load(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"));
            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "Entities", "test_entity", "Entity.xml"));

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
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var originalDoc = XDocument.Load(Path.Combine(SamplePath, "OptionSets", "tp_teststatus.xml"));
            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "OptionSets", "tp_teststatus.xml"));

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
    public void RoundtripSampleRepo_PreservesOptionLabelIndentation()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-indent-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenXml = File.ReadAllText(Path.Combine(outputPath, "OptionSets", "tp_teststatus.xml"));
            Assert.Contains(
                """
                  <options>
                    <option value="100000000" ExternalValue="" IsHidden="0">
                """.ReplaceLineEndings(),
                writtenXml.ReplaceLineEndings());
            Assert.Contains(
                """
                      <labels>
                        <label description="Active" languagecode="1033" />
                      </labels>
                """.ReplaceLineEndings(),
                writtenXml.ReplaceLineEndings());
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
            var entity1 = workspace.FindEntity("test_entity")!;
            var entity2 = workspace2.FindEntity("test_entity")!;
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

    [Fact]
    public void Roundtrip_PreservesDistinctRequiredLevelValues()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-requiredlevel-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-requiredlevel-out-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(SamplePath, inputPath);

            var entityFile = Path.Combine(inputPath, "Entities", "test_entity", "Entity.xml");
            var entityDoc = XDocument.Load(entityFile);
            var attributesByLogicalName = entityDoc.Root!
                .Element("EntityInfo")!
                .Element("entity")!
                .Element("attributes")!
                .Elements("attribute")
                .ToDictionary(
                    attribute => attribute.Element("LogicalName")!.Value,
                    attribute => attribute);
            attributesByLogicalName["tp_name"].Element("RequiredLevel")!.Value = "systemrequired";
            attributesByLogicalName["tp_count"].Element("RequiredLevel")!.Value = "applicationrequired";
            entityDoc.Save(entityFile);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);

            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "Entities", "test_entity", "Entity.xml"));
            var writtenRequiredLevels = writtenDoc.Root!
                .Element("EntityInfo")!
                .Element("entity")!
                .Element("attributes")!
                .Elements("attribute")
                .Select(attribute => attribute.Element("RequiredLevel")!.Value)
                .ToList();

            Assert.Contains("systemrequired", writtenRequiredLevels);
            Assert.Contains("applicationrequired", writtenRequiredLevels);
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_PreservesNonBooleanManagedValue()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-managed-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-managed-out-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(SamplePath, inputPath);

            var solutionFile = Path.Combine(inputPath, "Other", "Solution.xml");
            var solutionDoc = XDocument.Load(solutionFile);
            solutionDoc.Root!
                .Element("SolutionManifest")!
                .Element("Managed")!
                .Value = "2";
            solutionDoc.Save(solutionFile);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);

            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenManagedValue = XDocument.Load(Path.Combine(outputPath, "Other", "Solution.xml"))
                .Root!
                .Element("SolutionManifest")!
                .Element("Managed")!
                .Value;

            Assert.Equal("2", writtenManagedValue);
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_PreservesPassthroughWorkspaceFiles()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-passthrough-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-passthrough-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(inputPath, "Other"));
            Directory.CreateDirectory(Path.Combine(inputPath, "Entities", "account", "RibbonDiffXml"));

            File.WriteAllText(Path.Combine(inputPath, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0.0.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            var customizationsXml =
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <Entities />
                  <RibbonDiffXml />
                </ImportExportXml>
                """;
            File.WriteAllText(Path.Combine(inputPath, "Other", "Customizations.xml"), customizationsXml);

            var ribbonDiffXml =
                """
                <?xml version="1.0" encoding="utf-8"?>
                <RibbonDiffXml>
                  <CustomActions />
                </RibbonDiffXml>
                """;
            File.WriteAllText(Path.Combine(inputPath, "Entities", "account", "RibbonDiffXml", "RibbonDiff.xml"), ribbonDiffXml);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);
            var ribbon = Assert.Single(workspace.Ribbons);
            Assert.Equal("account", ribbon.EntityLogicalName);
            Assert.Equal("RibbonDiffXml", ribbon.Body?.Name);

            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            Assert.Equal(customizationsXml.ReplaceLineEndings(), File.ReadAllText(Path.Combine(outputPath, "Other", "Customizations.xml")).ReplaceLineEndings());
            Assert.Equal(ribbonDiffXml.ReplaceLineEndings(), File.ReadAllText(Path.Combine(outputPath, "Entities", "account", "RibbonDiffXml", "RibbonDiff.xml")).ReplaceLineEndings());
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_PerEntityRelationships_PreservesStructuredDetails()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-relationships-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-relationships-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(inputPath, "Other", "Relationships"));
            File.WriteAllText(Path.Combine(inputPath, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0.0.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                  </SolutionManifest>
                </ImportExportXml>
                """);
            File.WriteAllText(Path.Combine(inputPath, "Other", "Relationships.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships>
                  <EntityRelationship Name="account_contact_parentcustomerid" />
                </EntityRelationships>
                """);
            File.WriteAllText(Path.Combine(inputPath, "Other", "Relationships", "account.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships>
                  <EntityRelationship Name="account_contact_parentcustomerid">
                    <EntityRelationshipType>OneToMany</EntityRelationshipType>
                    <IsCustomizable>1</IsCustomizable>
                    <IntroducedVersion>1.0.0.0</IntroducedVersion>
                    <IsHierarchical>0</IsHierarchical>
                    <ReferencingEntityName>contact</ReferencingEntityName>
                    <ReferencedEntityName>account</ReferencedEntityName>
                    <CascadeAssign>NoCascade</CascadeAssign>
                    <CascadeDelete>Cascade</CascadeDelete>
                    <CascadeArchive>RemoveLink</CascadeArchive>
                    <CascadeReparent>NoCascade</CascadeReparent>
                    <CascadeShare>NoCascade</CascadeShare>
                    <CascadeUnshare>NoCascade</CascadeUnshare>
                    <CascadeRollupView>NoCascade</CascadeRollupView>
                    <IsValidForAdvancedFind>1</IsValidForAdvancedFind>
                    <ReferencingAttributeName>parentcustomerid</ReferencingAttributeName>
                    <EntityRelationshipRoles>
                      <EntityRelationshipRole>
                        <NavPaneDisplayOption>UseCollectionName</NavPaneDisplayOption>
                        <NavPaneArea>Details</NavPaneArea>
                        <NavPaneOrder>10000</NavPaneOrder>
                        <NavigationPropertyName>parentcustomerid_account</NavigationPropertyName>
                        <RelationshipRoleType>1</RelationshipRoleType>
                      </EntityRelationshipRole>
                    </EntityRelationshipRoles>
                  </EntityRelationship>
                </EntityRelationships>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "Other", "Relationships", "account.xml"));
            var relationship = writtenDoc.Root!.Element("EntityRelationship")!;
            Assert.Equal("OneToMany", relationship.Element("EntityRelationshipType")!.Value);
            Assert.Equal("contact", relationship.Element("ReferencingEntityName")!.Value);
            Assert.Equal("account", relationship.Element("ReferencedEntityName")!.Value);
            Assert.Equal("Cascade", relationship.Element("CascadeDelete")!.Value);
            Assert.Equal("parentcustomerid_account", relationship.Element("EntityRelationshipRoles")!.Element("EntityRelationshipRole")!.Element("NavigationPropertyName")!.Value);
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_Forms_WritesMergeableBodyFromModel()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-form-body-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-form-body-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(inputPath, "Other"));
            Directory.CreateDirectory(Path.Combine(inputPath, "Entities", "account", "FormXml", "main"));
            File.WriteAllText(Path.Combine(inputPath, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0.0.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                  </SolutionManifest>
                </ImportExportXml>
                """);
            File.WriteAllText(Path.Combine(inputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <forms>
                  <systemform>
                    <formid>{11111111-1111-1111-1111-111111111111}</formid>
                    <form>
                      <tabs>
                        <tab id="base-tab" />
                      </tabs>
                    </form>
                  </systemform>
                </forms>
                """);

            var workspace = new XmlWorkspaceReader().Load(inputPath);
            var form = Assert.Single(workspace.Forms);
            var tabs = FindFirst(form.Body!, "tabs")!;
            var addedTab = new MergeableNode { Name = "tab" };
            addedTab.Attributes["id"] = "added-tab";
            addedTab.Attributes["ordinalvalue"] = "10";
            tabs.Children.Add(addedTab);

            new XmlWorkspaceWriter().Write(workspace, outputPath);

            var written = XDocument.Load(Path.Combine(outputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}.xml"));
            var writtenTabs = written.Descendants("tab").ToList();
            Assert.Equal(2, writtenTabs.Count);
            Assert.Equal("added-tab", writtenTabs[1].Attribute("id")?.Value);
            Assert.Equal("10", writtenTabs[1].Attribute("ordinalvalue")?.Value);
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_ManagedFilesWriteBackToManagedPaths()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-managed-choice-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-managed-choice-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(inputPath, "Other"));
            Directory.CreateDirectory(Path.Combine(inputPath, "Entities", "account", "FormXml", "main"));
            Directory.CreateDirectory(Path.Combine(inputPath, "AppModules", "test_app"));
            Directory.CreateDirectory(Path.Combine(inputPath, "AppModuleSiteMaps", "test_app"));

            File.WriteAllText(Path.Combine(inputPath, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0.0.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            File.WriteAllText(Path.Combine(inputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <forms>
                  <systemform>
                    <formid>{11111111-1111-1111-1111-111111111111}</formid>
                    <LocalizedNames>
                      <LocalizedName description="Base Form" languagecode="1033" />
                    </LocalizedNames>
                  </systemform>
                </forms>
                """);
            File.WriteAllText(Path.Combine(inputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}_managed.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <forms>
                  <systemform>
                    <formid>{11111111-1111-1111-1111-111111111111}</formid>
                    <LocalizedNames>
                      <LocalizedName description="Managed Form" languagecode="1033" />
                    </LocalizedNames>
                  </systemform>
                </forms>
                """);

            File.WriteAllText(Path.Combine(inputPath, "AppModules", "test_app", "AppModule.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModule>
                  <UniqueName>test_app</UniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Base App" languagecode="1033" />
                  </LocalizedNames>
                </AppModule>
                """);
            File.WriteAllText(Path.Combine(inputPath, "AppModules", "test_app", "AppModule_managed.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModule>
                  <UniqueName>test_app</UniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Managed App" languagecode="1033" />
                  </LocalizedNames>
                </AppModule>
                """);

            File.WriteAllText(Path.Combine(inputPath, "AppModuleSiteMaps", "test_app", "AppModuleSiteMap.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModuleSiteMap>
                  <SiteMapUniqueName>test_app_sitemap</SiteMapUniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Base SiteMap" languagecode="1033" />
                  </LocalizedNames>
                  <SiteMap />
                </AppModuleSiteMap>
                """);
            File.WriteAllText(Path.Combine(inputPath, "AppModuleSiteMaps", "test_app", "AppModuleSiteMap_managed.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModuleSiteMap>
                  <SiteMapUniqueName>test_app_sitemap</SiteMapUniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Managed SiteMap" languagecode="1033" />
                  </LocalizedNames>
                  <SiteMap />
                </AppModuleSiteMap>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);
            workspace.Forms[0].DisplayName = new Label("Updated Managed Form");
            workspace.AppModules[0].DisplayName = new Label("Updated Managed App");
            workspace.SiteMaps[0].DisplayName = new Label("Updated Managed SiteMap");

            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            Assert.True(File.Exists(Path.Combine(outputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}_managed.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "AppModules", "test_app", "AppModule_managed.xml")));
            Assert.True(File.Exists(Path.Combine(outputPath, "AppModuleSiteMaps", "test_app", "AppModuleSiteMap_managed.xml")));

            Assert.False(File.Exists(Path.Combine(outputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}.xml")));
            Assert.False(File.Exists(Path.Combine(outputPath, "AppModules", "test_app", "AppModule.xml")));
            Assert.False(File.Exists(Path.Combine(outputPath, "AppModuleSiteMaps", "test_app", "AppModuleSiteMap.xml")));

            Assert.Contains("Updated Managed Form", File.ReadAllText(Path.Combine(outputPath, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}_managed.xml")));
            Assert.Contains("Updated Managed App", File.ReadAllText(Path.Combine(outputPath, "AppModules", "test_app", "AppModule_managed.xml")));
            Assert.Contains("Updated Managed SiteMap", File.ReadAllText(Path.Combine(outputPath, "AppModuleSiteMaps", "test_app", "AppModuleSiteMap_managed.xml")));
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Roundtrip_WebResourceWithBlankDisplayName_RemovesDisplayNameElement()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-webresource-in-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-webresource-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(inputPath, "Other"));
            Directory.CreateDirectory(Path.Combine(inputPath, "WebResources"));

            File.WriteAllText(Path.Combine(inputPath, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0.0.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            File.WriteAllText(Path.Combine(inputPath, "WebResources", "test_resource.js.data.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <WebResource>
                  <WebResourceId>{c1b2c3d4-0000-0000-0000-000000000003}</WebResourceId>
                  <Name>test_resource.js</Name>
                  <DisplayName>Test Resource</DisplayName>
                  <WebResourceType>3</WebResourceType>
                  <IsCustomizable>1</IsCustomizable>
                  <CanBeDeleted>1</CanBeDeleted>
                  <IsHidden>0</IsHidden>
                  <IsEnabledForMobileClient>0</IsEnabledForMobileClient>
                  <IsAvailableForMobileOffline>0</IsAvailableForMobileOffline>
                </WebResource>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(inputPath);
            workspace.WebResources[0].DisplayName = new Label("");

            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "WebResources", "test_resource.js.data.xml"));
            Assert.Null(writtenDoc.Root!.Element("DisplayName"));
        }
        finally
        {
            if (Directory.Exists(inputPath)) Directory.Delete(inputPath, true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Fact]
    public void Write_InMemoryWebResourceWithBlankDisplayName_DoesNotWriteDisplayNameElement()
    {
        var workspace = new Workspace("in-memory");
        workspace.Solution = new Solution
        {
            UniqueName = "InMemorySolution",
            Version = "1.0",
            Publisher = new Publisher { UniqueName = "test", Prefix = "test" }
        };
        workspace.AddWebResource(new WebResourceMetadata
        {
            WebResourceId = "{c1b2c3d4-0000-0000-0000-000000000003}",
            Name = "test_resource.js",
            DisplayName = new Label(""),
            WebResourceType = 3,
            IsCustomizable = true,
            CanBeDeleted = true
        });

        var outputPath = Path.Combine(Path.GetTempPath(), $"writer-webresource-{Guid.NewGuid():N}");
        try
        {
            var writer = new XmlWorkspaceWriter();
            writer.Write(workspace, outputPath);

            var writtenDoc = XDocument.Load(Path.Combine(outputPath, "WebResources", "test_resource.js.data.xml"));
            Assert.Null(writtenDoc.Root!.Element("DisplayName"));
        }
        finally
        {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourcePath, destinationPath, StringComparison.Ordinal));
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var destinationFile = file.Replace(sourcePath, destinationPath, StringComparison.Ordinal);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destinationFile);
        }
    }

    private static MergeableNode? FindFirst(MergeableNode root, string name)
    {
        if (root.Name == name) return root;
        foreach (var child in root.Children)
        {
            var result = FindFirst(child, name);
            if (result != null) return result;
        }

        return null;
    }
}
