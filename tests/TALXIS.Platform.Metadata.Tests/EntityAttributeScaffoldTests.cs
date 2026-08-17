using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EntityAttributeScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-attr-scaffold").FullName;
    private readonly string _tempDir;

    public EntityAttributeScaffoldTests()
    {
        _tempDir = Path.Combine(_root, ".template.temp");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        Directory.CreateDirectory(Path.Combine(_root, "Entities", EntityName));

        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SolutionManifest>
                <UniqueName>TestSolution</UniqueName>
                <LocalizedNames xsi:nil="true">
                </LocalizedNames>
                <RootComponents>
                  <RootComponent type="1" schemaName="udpp_warehouseitem" behavior="0" />
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);

        WriteEntityXml(withLocalizedNames: false);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteEntityXml(bool withLocalizedNames)
    {
        var localizedNames = withLocalizedNames ? "<LocalizedNames><LocalizedName description=\"Warehouse Item\" languagecode=\"1033\" /></LocalizedNames>" : "";
        File.WriteAllText(Path.Combine(_root, "Entities", EntityName, "Entity.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Name LocalizedName="Warehouse Item" OriginalName="Warehouse Item">{EntityName}</Name>
              <EntityInfo>
                <entity Name="{EntityName}">
                  {localizedNames}
                  <attributes>
                    <attribute PhysicalName="udpp_zebra">
                      <Type>nvarchar</Type>
                      <Name>udpp_zebra</Name>
                      <LogicalName>udpp_zebra</LogicalName>
                    </attribute>
                  </attributes>
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }

    private string WriteAttributeFile(string logicalName, string extraXml = "")
    {
        var path = Path.Combine(_tempDir, "attribute.xml");
        File.WriteAllText(path, $"""
            <attribute PhysicalName="{logicalName}">
                <Type>nvarchar</Type>
                <Name>{logicalName}</Name>
                <LogicalName>{logicalName}</LogicalName>
                {extraXml}
            </attribute>
            """);
        return path;
    }

    private EntityAttributeScaffoldRequest Request(string attributeFile) => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        AttributeFilePath = attributeFile,
    };

    private XDocument EntityXml() => XDocument.Load(Path.Combine(_root, "Entities", EntityName, "Entity.xml"));

    [Fact]
    public void Apply_AppendsAttributeToEntityXml()
    {
        var result = EntityAttributeScaffold.Apply(Request(WriteAttributeFile("udpp_alpha")));

        Assert.Empty(result.Warnings);
        var names = EntityXml().Descendants("attribute").Select(a => a.Element("LogicalName")?.Value).ToList();
        Assert.Contains("udpp_alpha", names);
    }

    [Fact]
    public void Apply_ExistingAttribute_SkipsWithWarning()
    {
        var result = EntityAttributeScaffold.Apply(Request(WriteAttributeFile("udpp_zebra")));

        Assert.Single(result.Warnings);
        Assert.Contains("already exists", result.Warnings[0]);
        Assert.Single(EntityXml().Descendants("attribute"), a => a.Element("LogicalName")?.Value == "udpp_zebra");
    }

    [Fact]
    public void Apply_SortsAttributesByPhysicalName()
    {
        EntityAttributeScaffold.Apply(Request(WriteAttributeFile("udpp_alpha")));

        var physicalNames = EntityXml().Descendants("attribute").Select(a => a.Attribute("PhysicalName")?.Value).ToList();
        Assert.Equal(["udpp_alpha", "udpp_zebra"], physicalNames);
    }

    [Fact]
    public void Apply_LocalOptions_InjectedIntoAttributeOptionSet()
    {
        var attributeFile = WriteAttributeFile("udpp_status", """
            <optionset Name="udpp_warehouseitem_udpp_status">
              <OptionSetType>picklist</OptionSetType>
              <options>
              </options>
            </optionset>
            """);
        var request = Request(attributeFile);
        request.OptionSetOptions = "Active:100000000,Inactive";

        EntityAttributeScaffold.Apply(request);

        var options = EntityXml().Descendants("attribute")
            .Single(a => a.Element("LogicalName")?.Value == "udpp_status")
            .Descendants("option").ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal("100000000", options[0].Attribute("value")?.Value);
        Assert.Equal("Active", options[0].Descendants("label").First().Attribute("description")?.Value);
        Assert.Equal("100000000", options[1].Attribute("value")?.Value);
        Assert.Equal("Inactive", options[1].Descendants("label").First().Attribute("description")?.Value);
    }

    [Fact]
    public void Apply_AutoIncrementOptions_StartAt100000000()
    {
        var attributeFile = WriteAttributeFile("udpp_status", "<optionset Name=\"x\"><options></options></optionset>");
        var request = Request(attributeFile);
        request.OptionSetOptions = "One,Two,Three";

        EntityAttributeScaffold.Apply(request);

        var values = EntityXml().Descendants("option").Select(o => o.Attribute("value")?.Value).ToList();
        Assert.Equal(["100000000", "100000001", "100000002"], values);
    }

    [Fact]
    public void Apply_GlobalOptionSet_FillsFileAndRegistersRootComponent()
    {
        var optionSetDir = Path.Combine(_root, "OptionSets");
        Directory.CreateDirectory(optionSetDir);
        var optionSetFile = Path.Combine(optionSetDir, "udpp_grade.xml");
        File.WriteAllText(optionSetFile, """
            <?xml version="1.0" encoding="utf-8"?>
            <optionset Name="udpp_grade" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <OptionSetType>picklist</OptionSetType>
              <IsGlobal>1</IsGlobal>
              <options>
              </options>
            </optionset>
            """);

        var request = Request(WriteAttributeFile("udpp_grade"));
        request.OptionSetOptions = "Gold,Silver";
        request.GlobalOptionSetFilePath = optionSetFile;
        request.GlobalOptionSetSchemaName = "udpp_grade";

        EntityAttributeScaffold.Apply(request);

        var optionSet = XDocument.Load(optionSetFile);
        Assert.Equal(2, optionSet.Descendants("option").Count());

        var solution = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"));
        var component = solution.Descendants("RootComponent").Single(c => c.Attribute("type")?.Value == "9");
        Assert.Equal("udpp_grade", component.Attribute("schemaName")?.Value);
        Assert.Equal("0", component.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_GlobalOptionSetRootComponent_IsIdempotent()
    {
        var optionSetDir = Path.Combine(_root, "OptionSets");
        Directory.CreateDirectory(optionSetDir);
        var optionSetFile = Path.Combine(optionSetDir, "udpp_grade.xml");
        File.WriteAllText(optionSetFile, "<optionset Name=\"udpp_grade\"><options></options></optionset>");

        var request = Request(WriteAttributeFile("udpp_grade"));
        request.OptionSetOptions = "Gold";
        request.GlobalOptionSetFilePath = optionSetFile;
        request.GlobalOptionSetSchemaName = "udpp_grade";
        EntityAttributeScaffold.Apply(request);

        var second = Request(WriteAttributeFile("udpp_grade2"));
        second.OptionSetOptions = "Gold";
        second.GlobalOptionSetFilePath = optionSetFile;
        second.GlobalOptionSetSchemaName = "udpp_grade";
        EntityAttributeScaffold.Apply(second);

        var solution = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"));
        Assert.Single(solution.Descendants("RootComponent"), c => c.Attribute("type")?.Value == "9");
    }

    [Fact]
    public void Apply_MoneySupport_StubEntity_AddsOnlyBaseAttribute()
    {
        var request = Request(WriteAttributeFile("udpp_price"));
        request.MoneyBaseAttributeFilePath = WriteSupportFile("money-base.xml", "udpp_price_base", "udpp_price_Base");
        request.CurrencyAttributeFilePath = WriteSupportFile("currency.xml", "transactioncurrencyid", "TransactionCurrencyId");
        request.ExchangeRateAttributeFilePath = WriteSupportFile("exchange.xml", "exchangerate", "ExchangeRate");

        EntityAttributeScaffold.Apply(request);

        var names = EntityXml().Descendants("attribute").Select(a => a.Element("LogicalName")?.Value).ToList();
        Assert.Contains("udpp_price_base", names);
        Assert.DoesNotContain("transactioncurrencyid", names);
        Assert.DoesNotContain("exchangerate", names);
    }

    [Fact]
    public void Apply_MoneySupport_FullEntity_AddsCurrencyAndExchangeRate()
    {
        WriteEntityXml(withLocalizedNames: true);

        var request = Request(WriteAttributeFile("udpp_price"));
        request.MoneyBaseAttributeFilePath = WriteSupportFile("money-base.xml", "udpp_price_base", "udpp_price_Base");
        request.CurrencyAttributeFilePath = WriteSupportFile("currency.xml", "transactioncurrencyid", "TransactionCurrencyId");
        request.ExchangeRateAttributeFilePath = WriteSupportFile("exchange.xml", "exchangerate", "ExchangeRate");

        EntityAttributeScaffold.Apply(request);

        var names = EntityXml().Descendants("attribute").Select(a => a.Element("LogicalName")?.Value).ToList();
        Assert.Contains("udpp_price_base", names);
        Assert.Contains("transactioncurrencyid", names);
        Assert.Contains("exchangerate", names);
    }

    [Fact]
    public void Apply_LookupRelationship_CreatesBothFiles()
    {
        var request = Request(WriteAttributeFile("udpp_supplier"));
        request.LookupRelationshipFilePath = WriteRelationshipFile("udpp_account_udpp_warehouseitem_supplier");
        request.LookupRelationshipName = "udpp_account_udpp_warehouseitem_supplier";
        request.ReferencedEntityName = "account";

        EntityAttributeScaffold.Apply(request);

        var referenced = XDocument.Load(Path.Combine(_root, "Other", "Relationships", "account.xml"));
        var relationship = referenced.Descendants("EntityRelationship").Single();
        Assert.Equal("udpp_account_udpp_warehouseitem_supplier", relationship.Attribute("Name")?.Value);
        Assert.Equal("OneToMany", relationship.Element("EntityRelationshipType")?.Value);

        var index = XDocument.Load(Path.Combine(_root, "Other", "Relationships.xml"));
        var stub = index.Descendants("EntityRelationship").Single();
        Assert.Equal("udpp_account_udpp_warehouseitem_supplier", stub.Attribute("Name")?.Value);
        Assert.False(stub.HasElements);
    }

    [Fact]
    public void Apply_LookupRelationship_IsIdempotent()
    {
        var request = Request(WriteAttributeFile("udpp_supplier"));
        request.LookupRelationshipFilePath = WriteRelationshipFile("udpp_rel");
        request.LookupRelationshipName = "udpp_rel";
        request.ReferencedEntityName = "account";
        EntityAttributeScaffold.Apply(request);

        var second = Request(WriteAttributeFile("udpp_supplier2"));
        second.LookupRelationshipFilePath = WriteRelationshipFile("udpp_rel");
        second.LookupRelationshipName = "udpp_rel";
        second.ReferencedEntityName = "account";
        var result = EntityAttributeScaffold.Apply(second);

        Assert.Equal(2, result.Warnings.Count);
        var referenced = XDocument.Load(Path.Combine(_root, "Other", "Relationships", "account.xml"));
        Assert.Single(referenced.Descendants("EntityRelationship"));
    }

    [Fact]
    public void Apply_NormalizesNilTagsInSolutionXml()
    {
        EntityAttributeScaffold.Apply(Request(WriteAttributeFile("udpp_alpha")));

        var content = File.ReadAllText(Path.Combine(_root, "Other", "Solution.xml"));
        Assert.Contains("xsi:nil=\"true\"></LocalizedNames>", content);
    }

    private string WriteSupportFile(string fileName, string logicalName, string physicalName)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, $"""
            <attribute PhysicalName="{physicalName}">
                <Type>money</Type>
                <Name>{logicalName}</Name>
                <LogicalName>{logicalName}</LogicalName>
            </attribute>
            """);
        return path;
    }

    private string WriteRelationshipFile(string relationshipName)
    {
        var path = Path.Combine(_tempDir, "lookup-relationship.xml");
        File.WriteAllText(path, $"""
            <EntityRelationship Name="{relationshipName}">
                <EntityRelationshipType>OneToMany</EntityRelationshipType>
                <ReferencingEntityName>{EntityName}</ReferencingEntityName>
                <ReferencedEntityName>account</ReferencedEntityName>
            </EntityRelationship>
            """);
        return path;
    }
}
