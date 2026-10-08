using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtMetadataValidatorTests
{
    [Fact]
    public void PackageThatMatchesTheMetadata_HasNoFindings()
    {
        var metadata = Metadata(Table("account"), Table("talxis_contract"));

        Assert.Empty(new CmtMetadataValidator().Validate(Package("account", "talxis_contract"), metadata));
    }

    [Fact]
    public void TableMissingFromTheMetadata_IsAnErrorInTheCmtDataStage()
    {
        var finding = Assert.Single(new CmtMetadataValidator().Validate(Package("account", "talxis_contracts"), Metadata(Table("account"), Table("talxis_contract"))));

        Assert.Equal(ValidationDiagnostics.CmtEntityNotInMetadata, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Equal(ValidationStage.CmtData, finding.Stage);
        Assert.Contains("'talxis_contracts'", finding.Message);
    }

    [Fact]
    public void TableMissingFromPartialMetadata_CanBeLoweredToAWarning()
    {
        var options = new CmtMetadataValidationOptions { UnknownEntitySeverity = ValidationSeverity.Warning };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(Package("account", "talxis_contract"), Metadata(Table("talxis_contract")), options));

        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Theory]
    [InlineData("talxis_log", false, true)]
    [InlineData("talxis_log", true, false)]
    [InlineData("talxis_log", null, false)]
    [InlineData("post", false, false)]
    public void TableThatDoesNotAcceptImports_IsAnError(string table, bool? isImportable, bool reported)
    {
        var metadata = Metadata(Table(table, isImportable));

        var findings = new CmtMetadataValidator().Validate(Package(table), metadata);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtEntityNotInMetadata && r.Severity == ValidationSeverity.Error));
    }

    [Theory]
    [InlineData("talxis_amount", true)]
    [InlineData("talxis_note", false)]
    [InlineData("versionnumber", false)]
    public void FieldThatIsNotAColumnOfTheTable_IsAnError(string field, bool reported)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField(field, CmtFieldTypes.String);
        var table = Table("talxis_contract");
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_note" });

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table));

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata && r.Severity == ValidationSeverity.Error && r.Message.Contains($"'talxis_contract.{field}'")));
    }

    [Fact]
    public void FieldWhoseNameDiffersOnlyByCase_IsLeftToTheLowercaseRule()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_Note", CmtFieldTypes.String);
        var table = Table("talxis_contract");
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_note" });

        Assert.Empty(new CmtMetadataValidator().Validate(package, Metadata(table)));
    }

    [Theory]
    [InlineData("string", false)]
    [InlineData("optionsetvalue", true)]
    [InlineData("lookup", true)]
    public void FieldDeclaredWithAnotherTypeThanItsColumn_IsAnError(string declared, bool reported)
    {
        var findings = ValidateField(new StringAttributeMetadata { LogicalName = "talxis_note" }, declared);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldTypeMismatch && r.Severity == ValidationSeverity.Error));
    }

    [Theory]
    [InlineData(LookupKind.Lookup, "entityreference", false)]
    [InlineData(LookupKind.Lookup, "lookup", false)]
    [InlineData(LookupKind.Customer, "customer", false)]
    [InlineData(LookupKind.Customer, "entityreference", false)]
    [InlineData(LookupKind.Owner, "owner", false)]
    [InlineData(LookupKind.Owner, "entityreference", true)]
    [InlineData(LookupKind.Lookup, "string", true)]
    public void LookupField_AcceptsTheImporterSynonyms(LookupKind kind, string declared, bool reported)
    {
        var findings = ValidateField(new LookupAttributeMetadata { LogicalName = "talxis_note", LookupKind = kind }, declared);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldTypeMismatch));
    }

    [Theory]
    [InlineData("filedata")]
    [InlineData("file")]
    public void FileField_AcceptsTheTalxisSpelling(string declared)
    {
        Assert.Empty(ValidateField(new FileAttributeMetadata { LogicalName = "talxis_note" }, declared));
    }

    private static IReadOnlyList<ValidationResult> ValidateField(AttributeMetadata column, string declaredType)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField(column.LogicalName, declaredType);
        var table = Table("talxis_contract");
        table.AddAttribute(column);
        return new CmtMetadataValidator().Validate(package, Metadata(table));
    }

    private static CmtPackage Package(params string[] tables)
    {
        var schema = new CmtDataSchema();
        foreach (var table in tables) schema.AddEntity(table, $"{table}id", "name").AddField("name", CmtFieldTypes.String, updateCompare: true);
        return new CmtPackage(schema);
    }

    private static EntityMetadata Table(string logicalName, bool? isImportable = null)
    {
        var table = new EntityMetadata { LogicalName = logicalName, PrimaryIdAttribute = $"{logicalName}id", PrimaryNameAttribute = "name", IsImportable = isImportable };
        table.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = $"{logicalName}id" });
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "name" });
        return table;
    }

    private static Workspace Metadata(params EntityMetadata[] tables)
    {
        var workspace = new Workspace("metadata");
        foreach (var table in tables) workspace.AddEntity(table);
        return workspace;
    }
}
