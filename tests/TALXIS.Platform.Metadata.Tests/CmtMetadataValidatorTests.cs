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

    [Theory]
    [InlineData("talxis_contract", "talxis_contract_account", false)]
    [InlineData("talxis_contract", "talxis_contract_contact", true)]
    [InlineData("account", "talxis_contract_account", true)]
    public void ManyToManyEntry_MustSitOnTheFirstTableOfItsRelationship(string entity, string intersect, bool reported)
    {
        var package = Package("talxis_contract", "account");
        package.Schema.FindEntity(entity)!.AddRelationship(new CmtSchemaRelationship
        {
            Name = intersect, IsManyToMany = true, RelatedEntityName = intersect, M2mTargetEntity = "account", M2mTargetEntityPrimaryKey = "accountid"
        });
        var metadata = Metadata(Table("talxis_contract"), Table("account"));
        metadata.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "talxis_contract_account", Entity1LogicalName = "talxis_contract", Entity2LogicalName = "account", IntersectEntityName = "talxis_contract_account"
        });

        var findings = new CmtMetadataValidator().Validate(package, metadata);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtManyToManyNotInMetadata && r.Severity == ValidationSeverity.Error));
    }

    [Theory]
    [InlineData("talxis_contractid", "name", 0)]
    [InlineData("talxis_title", "name", 1)]
    [InlineData("talxis_contractid", "talxis_title", 1)]
    [InlineData("talxis_title", "talxis_title", 2)]
    public void PrimaryFieldsThatAreNotTheTablePrimaryColumns_AreWarnings(string primaryIdField, string primaryNameField, int expected)
    {
        var schema = new CmtDataSchema();
        var entity = schema.AddEntity("talxis_contract", "talxis_contractid", primaryNameField);
        entity.PrimaryIdField = primaryIdField;

        var findings = new CmtMetadataValidator().Validate(new CmtPackage(schema), Metadata(Table("talxis_contract")))
            .Where(r => r.Code == ValidationDiagnostics.CmtPrimaryFieldMismatch).ToList();

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, r => Assert.Equal(ValidationSeverity.Warning, r.Severity));
    }

    [Fact]
    public void PrimaryNameTheMetadataDoesNotKnow_IsNotAMismatch()
    {
        var table = Table("talxis_contract");
        table.PrimaryNameAttribute = null;

        Assert.Empty(new CmtMetadataValidator().Validate(Package("talxis_contract"), Metadata(table)));
    }

    [Theory]
    [InlineData("account|contact", true, 0)]
    [InlineData("account", true, 0)]
    [InlineData("account|talxis_partner", true, 1)]
    [InlineData("*", true, 0)]
    [InlineData("talxis_partner", false, 0)]
    public void LookupTypeNamingATableTheColumnCannotPointTo_IsAWarning(string lookupType, bool relationshipsKnown, int expected)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_customerid", CmtFieldTypes.EntityReference, lookupType: lookupType);
        var table = Table("talxis_contract");
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_customerid", LookupKind = LookupKind.Customer });
        var metadata = Metadata(table);
        if (relationshipsKnown)
        {
            metadata.AddRelationship(ManyToOne("talxis_contract", "talxis_customerid", "account"));
            metadata.AddRelationship(ManyToOne("talxis_contract", "talxis_customerid", "contact"));
        }

        var findings = new CmtMetadataValidator().Validate(package, metadata).Where(r => r.Code == ValidationDiagnostics.CmtLookupTargetNotAllowed).ToList();

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, r => Assert.Equal(ValidationSeverity.Warning, r.Severity));
    }

    [Theory]
    [InlineData("talxis_contract_talxis_regionid_talxis_region", "talxis_regionid", "talxis_region", null)]
    [InlineData("talxis_contract_talxis_areaid_talxis_region", "talxis_areaid", "talxis_region", "is not a relationship in the metadata")]
    [InlineData("talxis_contract_talxis_regionid_talxis_region", "talxis_regionid", "talxis_country", "pointing to 'talxis_region'")]
    public void ManyToOneEntry_MustMatchARelationshipInTheMetadata(string name, string referencingAttribute, string referencedEntity, string? reported)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddRelationship(new CmtSchemaRelationship
        {
            Name = name, ReferencingEntity = "talxis_contract", ReferencingAttribute = referencingAttribute,
            ReferencedEntity = referencedEntity, ReferencedAttribute = $"{referencedEntity}id"
        });
        var metadata = Metadata(Table("talxis_contract"));
        metadata.AddRelationship(ManyToOne("talxis_contract", "talxis_regionid", "talxis_region"));

        var findings = new CmtMetadataValidator().Validate(package, metadata).Where(r => r.Code == ValidationDiagnostics.CmtRelationshipNotInMetadata).ToList();

        if (reported == null)
        {
            Assert.Empty(findings);
            return;
        }

        var finding = Assert.Single(findings);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains(reported, finding.Message);
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

    private static OneToManyRelationshipMetadata ManyToOne(string referencingEntity, string referencingAttribute, string referencedEntity) =>
        new()
        {
            SchemaName = $"{referencingEntity}_{referencingAttribute}_{referencedEntity}",
            ReferencingEntity = referencingEntity,
            ReferencingAttribute = referencingAttribute,
            ReferencedEntity = referencedEntity,
            ReferencedAttribute = $"{referencedEntity}id"
        };

    private static Workspace Metadata(params EntityMetadata[] tables)
    {
        var workspace = new Workspace("metadata");
        foreach (var table in tables) workspace.AddEntity(table);
        return workspace;
    }
}
