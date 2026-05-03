using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class MultiLanguageLabelTests
{
    private const string MinimalSolutionXml =
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
        """;

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), $"multilang-test-{Guid.NewGuid():N}");

    private static void WriteSolution(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Other"));
        File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"), MinimalSolutionXml);
    }

    [Fact]
    public void Reader_LoadsAllLanguages_FromEntity()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames>
                        <LocalizedName description="Test Entity" languagecode="1033" />
                        <LocalizedName description="Testentität" languagecode="1031" />
                        <LocalizedName description="Testovací entita" languagecode="1029" />
                      </LocalizedNames>
                      <LocalizedCollectionNames>
                        <LocalizedCollectionName description="Test Entities" languagecode="1033" />
                      </LocalizedCollectionNames>
                      <attributes />
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var entity = workspace.FindEntity("test_entity")!;

            Assert.Equal("Test Entity", entity.DisplayName[1033]);
            Assert.Equal("Testentität", entity.DisplayName[1031]);
            Assert.Equal("Testovací entita", entity.DisplayName[1029]);
            Assert.Equal(3, entity.DisplayName.LocalizedLabels.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Reader_LoadsAllLanguages_FromAttribute()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames>
                        <LocalizedName description="Test Entity" languagecode="1033" />
                      </LocalizedNames>
                      <LocalizedCollectionNames>
                        <LocalizedCollectionName description="Test Entities" languagecode="1033" />
                      </LocalizedCollectionNames>
                      <attributes>
                        <attribute PhysicalName="test_name">
                          <Type>nvarchar</Type>
                          <LogicalName>test_name</LogicalName>
                          <MaxLength>100</MaxLength>
                          <IsCustomField>1</IsCustomField>
                          <displaynames>
                            <displayname description="Name" languagecode="1033" />
                            <displayname description="Name (DE)" languagecode="1031" />
                            <displayname description="Název" languagecode="1029" />
                          </displaynames>
                        </attribute>
                      </attributes>
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var attr = workspace.FindEntity("test_entity")!.FindAttribute("test_name")!;

            Assert.Equal("Name", attr.DisplayName[1033]);
            Assert.Equal("Name (DE)", attr.DisplayName[1031]);
            Assert.Equal("Název", attr.DisplayName[1029]);
            Assert.Equal(3, attr.DisplayName.LocalizedLabels.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Reader_LoadsAllLanguages_FromOptionSetOption()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "OptionSets"));
            File.WriteAllText(Path.Combine(dir, "OptionSets", "test_status.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <optionset Name="test_status" localizedName="Test Status" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <OptionSetType>picklist</OptionSetType>
                  <IsGlobal>true</IsGlobal>
                  <IsCustomizable>1</IsCustomizable>
                  <options>
                    <option value="100000000" ExternalValue="" IsHidden="0">
                      <labels>
                        <label description="Active" languagecode="1033" />
                        <label description="Aktiv" languagecode="1031" />
                        <label description="Aktivní" languagecode="1029" />
                      </labels>
                    </option>
                  </options>
                </optionset>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var option = workspace.GlobalOptionSets[0].Options[0];

            Assert.Equal("Active", option.Label[1033]);
            Assert.Equal("Aktiv", option.Label[1031]);
            Assert.Equal("Aktivní", option.Label[1029]);
            Assert.Equal(3, option.Label.LocalizedLabels.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Reader_LoadsAllLanguages_FromDescription()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames>
                        <LocalizedName description="Test Entity" languagecode="1033" />
                      </LocalizedNames>
                      <LocalizedCollectionNames>
                        <LocalizedCollectionName description="Test Entities" languagecode="1033" />
                      </LocalizedCollectionNames>
                      <Descriptions>
                        <Description description="A test entity" languagecode="1033" />
                        <Description description="Eine Testentität" languagecode="1031" />
                        <Description description="Testovací entita" languagecode="1029" />
                      </Descriptions>
                      <attributes />
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var entity = workspace.FindEntity("test_entity")!;

            Assert.Equal("A test entity", entity.Description[1033]);
            Assert.Equal("Eine Testentität", entity.Description[1031]);
            Assert.Equal("Testovací entita", entity.Description[1029]);
            Assert.Equal(3, entity.Description.LocalizedLabels.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Writer_PreservesAllLanguages_Roundtrip()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames>
                        <LocalizedName description="Test Entity" languagecode="1033" />
                        <LocalizedName description="Testentität" languagecode="1031" />
                        <LocalizedName description="Testovací entita" languagecode="1029" />
                      </LocalizedNames>
                      <LocalizedCollectionNames>
                        <LocalizedCollectionName description="Test Entities" languagecode="1033" />
                      </LocalizedCollectionNames>
                      <attributes />
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            // Load → Write → Re-load
            var workspace = new XmlWorkspaceReader().Load(dir);

            var outputPath = CreateTempDir();
            try
            {
                new XmlWorkspaceWriter().Write(workspace, outputPath);
                var reloaded = new XmlWorkspaceReader().Load(outputPath);
                var entity = reloaded.FindEntity("test_entity")!;

                Assert.Equal("Test Entity", entity.DisplayName[1033]);
                Assert.Equal("Testentität", entity.DisplayName[1031]);
                Assert.Equal("Testovací entita", entity.DisplayName[1029]);
                Assert.Equal(3, entity.DisplayName.LocalizedLabels.Count);
            }
            finally { if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true); }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Writer_AddsNewLanguage()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
            File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="test_entity">
                      <EntitySetName>test_entities</EntitySetName>
                      <LocalizedNames>
                        <LocalizedName description="Test Entity" languagecode="1033" />
                      </LocalizedNames>
                      <LocalizedCollectionNames>
                        <LocalizedCollectionName description="Test Entities" languagecode="1033" />
                      </LocalizedCollectionNames>
                      <attributes />
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            // Load, add German, write
            var workspace = new XmlWorkspaceReader().Load(dir);
            var entity = workspace.FindEntity("test_entity")!;
            entity.DisplayName[1031] = "Testentität";

            var outputPath = CreateTempDir();
            try
            {
                new XmlWorkspaceWriter().Write(workspace, outputPath);

                // Verify the raw XML contains both language entries
                var doc = XDocument.Load(Path.Combine(outputPath, "Entities", "test_entity", "Entity.xml"));
                var names = doc.Root!
                    .Element("EntityInfo")!
                    .Element("entity")!
                    .Element("LocalizedNames")!
                    .Elements("LocalizedName")
                    .ToList();

                Assert.Equal(2, names.Count);

                var en = names.Single(e => e.Attribute("languagecode")!.Value == "1033");
                var de = names.Single(e => e.Attribute("languagecode")!.Value == "1031");
                Assert.Equal("Test Entity", en.Attribute("description")!.Value);
                Assert.Equal("Testentität", de.Attribute("description")!.Value);

                // Also verify round-trip via reader
                var reloaded = new XmlWorkspaceReader().Load(outputPath);
                var reloadedEntity = reloaded.FindEntity("test_entity")!;
                Assert.Equal("Test Entity", reloadedEntity.DisplayName[1033]);
                Assert.Equal("Testentität", reloadedEntity.DisplayName[1031]);
                Assert.Equal(2, reloadedEntity.DisplayName.LocalizedLabels.Count);
            }
            finally { if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true); }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
