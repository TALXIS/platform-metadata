using TALXIS.Platform.Metadata.Components;
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

    private static CmtPackage Package(params string[] tables)
    {
        var schema = new CmtDataSchema();
        foreach (var table in tables) schema.AddEntity(table, $"{table}id", "name");
        return new CmtPackage(schema);
    }

    private static EntityMetadata Table(string logicalName, bool? isImportable = null) =>
        new() { LogicalName = logicalName, PrimaryIdAttribute = $"{logicalName}id", PrimaryNameAttribute = "name", IsImportable = isImportable };

    private static Workspace Metadata(params EntityMetadata[] tables)
    {
        var workspace = new Workspace("metadata");
        foreach (var table in tables) workspace.AddEntity(table);
        return workspace;
    }
}
