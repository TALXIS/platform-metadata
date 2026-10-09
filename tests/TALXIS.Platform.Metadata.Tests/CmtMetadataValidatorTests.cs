using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtMetadataValidatorTests
{
    [Fact]
    public void MissingPackageOrMetadata_IsRejected()
    {
        Assert.Throws<ArgumentNullException>("package", () => new CmtMetadataValidator().Validate(null!, Metadata()));
        Assert.Throws<ArgumentNullException>("metadata", () => new CmtMetadataValidator().Validate(Package("account"), null!));
    }

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
        Assert.Contains("if the target environment does not have it", finding.Message);
    }

    [Theory]
    [InlineData("businessunit")]
    [InlineData("systemuser")]
    [InlineData("SystemUser")]
    public void SystemTableEveryEnvironmentHas_IsNotReportedAsMissing(string table)
    {
        Assert.Empty(new CmtMetadataValidator().Validate(Package(table), Metadata()));
    }

    [Fact]
    public void TableMissingFromPartialMetadata_CanBeLoweredToAWarning()
    {
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = ValidationSeverity.Warning };

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

    [Fact]
    public void TableThatDoesNotAcceptImports_GetsNoOtherFindings()
    {
        var package = Package("talxis_log");
        package.Schema.FindEntity("talxis_log")!.AddField("talxis_amount", CmtFieldTypes.Decimal);

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(Table("talxis_log", isImportable: false))));

        Assert.Equal(ValidationDiagnostics.CmtEntityNotInMetadata, finding.Code);
    }

    [Theory]
    [InlineData("talxis_amount", true)]
    [InlineData("talxis_note", false)]
    [InlineData("versionnumber", false)]
    [InlineData("VersionNumber", false)]
    public void FieldThatIsNotAColumnOfTheTable_IsAnError(string field, bool reported)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField(field, CmtFieldTypes.String);
        var table = Table("talxis_contract");
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_note" });

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table));

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata && r.Severity == ValidationSeverity.Error && r.Message.Contains($"'talxis_contract.{field}'")));
    }

    [Theory]
    [InlineData("talxis_totallength_state", false)]
    [InlineData("talxis_totallength_date", false)]
    [InlineData("owneridname", false)]
    [InlineData("owneridtype", false)]
    [InlineData("owneridyominame", false)]
    [InlineData("talxis_notes_state", true)]
    [InlineData("talxis_totallengthname", true)]
    public void HelperColumnsSolutionsLeaveOut_AreNotReportedAsMissing(string field, bool reported)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField(field, CmtFieldTypes.String);
        var table = Table("talxis_contract");
        table.AddAttribute(new DecimalAttributeMetadata { LogicalName = "talxis_totallength", SourceType = AttributeSourceType.Rollup });
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_notes" });
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "ownerid", LookupKind = LookupKind.Owner });

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table));

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata));
    }

    [Fact]
    public void ColumnDerivedFromAnother_IsNotChecked()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_parentidname", CmtFieldTypes.Number);
        var table = Table("talxis_contract");
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_parentidname", AttributeOf = "talxis_parentid", IsValidForCreate = false, IsValidForUpdate = false });

        Assert.Empty(new CmtMetadataValidator().Validate(package, Metadata(table)));
    }

    [Fact]
    public void TableNamedWithCapitalsInTheMetadata_IsNamedInLowercase()
    {
        var package = Package("account");
        package.Schema.FindEntity("account")!.AddField("talxis_amount", CmtFieldTypes.Decimal);
        var table = Table("account");
        table.LogicalName = "Account";

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(table)), r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata);

        Assert.DoesNotContain("'Account'", finding.Message);
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
    [InlineData("entityreference", true)]
    [InlineData("text", false)]
    [InlineData("String", false)]
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
    public void LookupField_LeavesTypesOutsideTheCmtVocabularyToTheStructuralRules(LookupKind kind, string declared, bool reported)
    {
        var findings = ValidateField(new LookupAttributeMetadata { LogicalName = "talxis_note", LookupKind = kind }, declared);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldTypeMismatch));
    }

    [Theory]
    [InlineData(LookupKind.Owner, "an owner lookup")]
    [InlineData(LookupKind.Customer, "a customer lookup")]
    [InlineData(LookupKind.Lookup, "a lookup")]
    public void TypeMismatchOnALookup_NamesTheKindOfLookup(LookupKind kind, string described)
    {
        var finding = Assert.Single(ValidateField(new LookupAttributeMetadata { LogicalName = "talxis_note", LookupKind = kind }, "string"), r => r.Code == ValidationDiagnostics.CmtFieldTypeMismatch);

        Assert.Contains($"the column is {described},", finding.Message);
    }

    [Theory]
    [InlineData("filedata")]
    [InlineData("file")]
    public void FileField_LeavesTheTalxisSpellingToTheStructuralRules(string declared)
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

    [Fact]
    public void ManyToManyEntryOnATableOutsideTheRelationship_SaysWhereItBelongs()
    {
        var package = Package("contact");
        package.Schema.FindEntity("contact")!.AddRelationship(new CmtSchemaRelationship { Name = "talxis_contract_account", IsManyToMany = true });
        var metadata = Metadata(new EntityMetadata { LogicalName = "contact" });
        metadata.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "talxis_contract_account_rel", Entity1LogicalName = "talxis_contract", Entity2LogicalName = "account", IntersectEntityName = "talxis_contract_account"
        });
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = ValidationSeverity.Warning };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, metadata, options), r => r.Code == ValidationDiagnostics.CmtManyToManyNotInMetadata);

        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("belongs to table 'talxis_contract'", finding.Message);
        Assert.DoesNotContain("only partly in the metadata", finding.Message);
    }

    [Fact]
    public void ManyToManyEntryNamedAfterTheRelationshipInsteadOfItsIntersectTable_SaysWhichNameToUse()
    {
        var package = Package("talxis_pricelistheader");
        package.Schema.FindEntity("talxis_pricelistheader")!.AddRelationship(new CmtSchemaRelationship
        {
            Name = "ntg_talxis_pricelistheader_talxis_contract", IsManyToMany = true, M2mTargetEntity = "talxis_contract", M2mTargetEntityPrimaryKey = "talxis_contractid"
        });
        var metadata = Metadata(new EntityMetadata { LogicalName = "talxis_pricelistheader" });
        metadata.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "ntg_talxis_pricelistheader_talxis_contract", Entity1LogicalName = "talxis_pricelistheader", Entity2LogicalName = "talxis_contract",
            IntersectEntityName = "ntg_talxis_contract_talxis_pricelistheader"
        });
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = ValidationSeverity.Warning };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, metadata, options), r => r.Code == ValidationDiagnostics.CmtManyToManyNotInMetadata);

        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("'ntg_talxis_contract_talxis_pricelistheader'", finding.Message);
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

        var table = Table("talxis_contract");
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_title" });

        var findings = new CmtMetadataValidator().Validate(new CmtPackage(schema), Metadata(table))
            .Where(r => r.Code == ValidationDiagnostics.CmtPrimaryFieldMismatch).ToList();

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, r => Assert.Equal(ValidationSeverity.Warning, r.Severity));
    }

    [Fact]
    public void PrimaryIdFieldThatIsNotAColumn_IsLeftToTheMissingFieldRule()
    {
        var schema = new CmtDataSchema();
        schema.AddEntity("talxis_contract", "talxis_contractidd", "name").AddField("name", CmtFieldTypes.String, updateCompare: true);

        var finding = Assert.Single(new CmtMetadataValidator().Validate(new CmtPackage(schema), Metadata(Table("talxis_contract"))));

        Assert.Equal(ValidationDiagnostics.CmtFieldNotInMetadata, finding.Code);
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
    [InlineData(" account | contact ", true, 0)]
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

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(null, false, false)]
    [InlineData(null, null, false)]
    public void ColumnClosedToCreateAndUpdate_IsAWarning(bool? validForCreate, bool? validForUpdate, bool reported)
    {
        var column = new StringAttributeMetadata { LogicalName = "talxis_note", IsValidForCreate = validForCreate, IsValidForUpdate = validForUpdate };

        var findings = ValidateField(column, CmtFieldTypes.String);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldNotWritable && r.Severity == ValidationSeverity.Warning));
    }

    [Theory]
    [InlineData("100000001", 0)]
    [InlineData("100000005", 1)]
    [InlineData("not-a-number", 0)]
    [InlineData("-1", 1)]
    public void ChoiceValueThatIsNotAnOption_IsAWarning(string value, int expected)
    {
        var findings = ValidateOptionValue(LocalChoice(), CmtFieldTypes.OptionSetValue, value);

        Assert.Equal(expected, findings.Count(r => r.Code == ValidationDiagnostics.CmtDataOptionValueUnknown && r.Severity == ValidationSeverity.Warning));
    }

    [Theory]
    [InlineData("[-1,100000000,100000001,-1]", 0)]
    [InlineData("100000000,100000001", 0)]
    [InlineData("[\"100000000\",\"100000001\"]", 0)]
    [InlineData("[-1,100000000,100000009,-1]", 1)]
    [InlineData("[\"100000009\"]", 1)]
    public void MultiSelectValue_IsReadInEveryFormat(string value, int expected)
    {
        var column = new MultiSelectPicklistAttributeMetadata { LogicalName = "talxis_size", OptionSet = LocalChoice().OptionSet };

        var findings = ValidateOptionValue(column, CmtFieldTypes.OptionSetValueCollection, value);

        Assert.Equal(expected, findings.Count(r => r.Code == ValidationDiagnostics.CmtDataOptionValueUnknown));
    }

    [Theory]
    [InlineData("2", 0)]
    [InlineData("987654", 1)]
    public void StatusValueThatIsNotAStatus_IsAWarning(string value, int expected)
    {
        var statuses = new OptionSetMetadata { Name = "talxis_contract_statuscode" };
        statuses.AddOption(new OptionMetadata { Value = 1 });
        statuses.AddOption(new OptionMetadata { Value = 2 });
        var column = new StatusAttributeMetadata { LogicalName = "statuscode", OptionSet = statuses };

        var findings = ValidateOptionValue(column, CmtFieldTypes.Status, value);

        Assert.Equal(expected, findings.Count(r => r.Code == ValidationDiagnostics.CmtDataOptionValueUnknown));
    }

    [Fact]
    public void GlobalChoice_TakesItsOptionsFromTheGlobalOptionSet()
    {
        var column = new PicklistAttributeMetadata { LogicalName = "talxis_size", IsGlobal = true, OptionSet = new OptionSetMetadata { Name = "talxis_size", IsGlobal = true } };
        var global = new OptionSetMetadata { Name = "talxis_size", IsGlobal = true };
        global.AddOption(new OptionMetadata { Value = 1 });

        var findings = ValidateOptionValue(column, CmtFieldTypes.OptionSetValue, "2", workspace => workspace.AddGlobalOptionSet(global));

        Assert.Single(findings, r => r.Code == ValidationDiagnostics.CmtDataOptionValueUnknown);
    }

    [Fact]
    public void ChoiceWhoseOptionsTheMetadataDoesNotList_IsNotChecked()
    {
        var column = new PicklistAttributeMetadata { LogicalName = "talxis_size", OptionSet = new OptionSetMetadata { Name = "talxis_size", IsGlobal = true } };

        Assert.Empty(ValidateOptionValue(column, CmtFieldTypes.OptionSetValue, "2"));
    }

    [Fact]
    public void ChoiceValuesOfALiquidEntity_AreNotChecked()
    {
        var findings = ValidateOptionValue(LocalChoice(), CmtFieldTypes.OptionSetValue, "9", configure: null, renderLiquid: true);

        Assert.Empty(findings);
    }

    [Fact]
    public void UnknownChoiceValues_AreReportedOncePerField()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_size", CmtFieldTypes.OptionSetValue);
        package.Data!.AddEntity("talxis_contract").AddRecord(Guid.NewGuid()).Set("talxis_size", "7");
        package.Data.FindEntity("talxis_contract")!.AddRecord(Guid.NewGuid()).Set("talxis_size", "8");
        var table = Table("talxis_contract");
        table.AddAttribute(LocalChoice());

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(table)));

        Assert.Contains("'7', '8' in 2 record(s)", finding.Message);
    }

    [Theory]
    [InlineData("account", ValidationSeverity.Error, 0)]
    [InlineData("systemuser", ValidationSeverity.Error, 0)]
    [InlineData("talxis_region", ValidationSeverity.Error, 1)]
    [InlineData("talxis_region", ValidationSeverity.Warning, 1)]
    public void LookupToATableNotInTheMetadata_TakesTheIncompleteMetadataSeverity(string lookupEntity, ValidationSeverity severity, int expected)
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_parentid", CmtFieldTypes.EntityReference);
        var records = package.Data!.AddEntity("talxis_contract");
        records.AddRecord(Guid.NewGuid()).Set("talxis_parentid", Guid.NewGuid().ToString(), lookupEntity);
        records.AddRecord(Guid.NewGuid()).Set("talxis_parentid", Guid.NewGuid().ToString(), lookupEntity);
        var table = Table("talxis_contract");
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_parentid" });
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = severity };

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table, Table("account")), options)
            .Where(r => r.Code == ValidationDiagnostics.CmtDataLookupEntityUnknown).ToList();

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, r => Assert.Equal(severity, r.Severity));
        Assert.All(findings, r => Assert.Contains("in 2 record(s)", r.Message));
    }

    [Fact]
    public void LookupToATableThePackageItselfImports_IsNotReportedAgain()
    {
        var package = Package("talxis_contract", "talxis_region");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_regionid", CmtFieldTypes.EntityReference);
        package.Data!.AddEntity("talxis_contract").AddRecord(Guid.NewGuid()).Set("talxis_regionid", Guid.NewGuid().ToString(), "talxis_region");
        var table = Table("talxis_contract");
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_regionid" });

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table));

        Assert.Single(findings, r => r.Code == ValidationDiagnostics.CmtEntityNotInMetadata);
        Assert.DoesNotContain(findings, r => r.Code == ValidationDiagnostics.CmtDataLookupEntityUnknown);
    }

    [Fact]
    public void LookupsCmtDoesNotImport_AreNotChecked()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_regionid", CmtFieldTypes.EntityReference);
        package.Data!.AddEntity("talxis_undeclared").AddRecord(Guid.NewGuid()).Set("talxis_regionid", Guid.NewGuid().ToString(), "talxis_region");
        package.Data.AddEntity("talxis_contract").AddRecord(Guid.NewGuid()).Set("talxis_notdeclared", Guid.NewGuid().ToString(), "talxis_region");
        var table = Table("talxis_contract");
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_regionid" });

        var findings = new CmtMetadataValidator().Validate(package, Metadata(table));

        Assert.DoesNotContain(findings, r => r.Code == ValidationDiagnostics.CmtDataLookupEntityUnknown);
    }

    [Fact]
    public void LookupsOfALiquidTable_AreNotChecked()
    {
        var package = Package("talxis_contract");
        var entity = package.Schema.FindEntity("talxis_contract")!;
        entity.AddField("talxis_regionid", CmtFieldTypes.EntityReference);
        entity.RenderLiquid = true;
        package.Data!.AddEntity("talxis_contract").AddRecord(Guid.NewGuid()).Set("talxis_regionid", Guid.NewGuid().ToString(), "talxis_region");
        var table = Table("talxis_contract");
        table.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_regionid" });

        Assert.Empty(new CmtMetadataValidator().Validate(package, Metadata(table)));
    }

    [Fact]
    public void MoreThanFiveUnknownChoiceValues_SaysHowManyAreNotListed()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddField("talxis_size", CmtFieldTypes.OptionSetValue);
        var records = package.Data!.AddEntity("talxis_contract");
        foreach (var value in new[] { "1", "2", "3", "4", "5", "6", "7" }) records.AddRecord(Guid.NewGuid()).Set("talxis_size", value);
        var table = Table("talxis_contract");
        table.AddAttribute(LocalChoice());

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(table)));

        Assert.Contains("'1', '2', '3', '4', '5' and 2 more in 7 record(s)", finding.Message);
    }

    private static PicklistAttributeMetadata LocalChoice()
    {
        var optionSet = new OptionSetMetadata { Name = "talxis_contract_talxis_size" };
        optionSet.AddOption(new OptionMetadata { Value = 100000000 });
        optionSet.AddOption(new OptionMetadata { Value = 100000001 });
        return new PicklistAttributeMetadata { LogicalName = "talxis_size", OptionSet = optionSet };
    }

    private static IReadOnlyList<ValidationResult> ValidateOptionValue(AttributeMetadata column, string type, string value, Action<Workspace>? configure = null, bool renderLiquid = false)
    {
        var package = Package("talxis_contract");
        var entity = package.Schema.FindEntity("talxis_contract")!;
        entity.AddField(column.LogicalName, type);
        entity.RenderLiquid = renderLiquid ? true : null;
        package.Data!.AddEntity("talxis_contract").AddRecord(Guid.NewGuid()).Set(column.LogicalName, value);
        var table = Table("talxis_contract");
        table.AddAttribute(column);
        var metadata = Metadata(table);
        configure?.Invoke(metadata);
        return new CmtMetadataValidator().Validate(package, metadata);
    }

    [Theory]
    [InlineData(ValidationSeverity.Error)]
    [InlineData(ValidationSeverity.Warning)]
    public void FieldsMissingFromATableOnlyPartlyInTheMetadata_AreReportedOnceForTheTable(ValidationSeverity severity)
    {
        var package = Package("talxis_contract");
        var entity = package.Schema.FindEntity("talxis_contract")!;
        entity.AddField("statecode", CmtFieldTypes.State);
        entity.AddField("ownerid", CmtFieldTypes.Owner);
        entity.AddField("talxis_amount", CmtFieldTypes.Decimal);
        var table = new EntityMetadata { LogicalName = "talxis_contract" };
        table.AddAttribute(new StringAttributeMetadata { LogicalName = "talxis_note" });
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = severity };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(table), options), r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata);

        Assert.Equal(severity, finding.Severity);
        Assert.Contains("fields 'name', 'talxis_amount' of table 'talxis_contract'", finding.Message);
        Assert.Contains("only partly in the metadata", finding.Message);
    }

    [Fact]
    public void OneFieldMissingFromATableOnlyPartlyInTheMetadata_IsNamedInTheSingular()
    {
        var table = new EntityMetadata { LogicalName = "talxis_contract" };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(Package("talxis_contract"), Metadata(table)), r => r.Code == ValidationDiagnostics.CmtFieldNotInMetadata);

        Assert.Contains("field 'name' of table 'talxis_contract' is not a column", finding.Message);
    }

    [Fact]
    public void ManyToManyMissingOnATableOnlyPartlyInTheMetadata_TakesTheIncompleteMetadataSeverity()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddRelationship(new CmtSchemaRelationship { Name = "talxis_contract_account", IsManyToMany = true });
        var table = new EntityMetadata { LogicalName = "talxis_contract" };
        var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = ValidationSeverity.Warning };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(table), options), r => r.Code == ValidationDiagnostics.CmtManyToManyNotInMetadata);

        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void LookupTypeOnATableOnlyPartlyInTheMetadata_SaysTheMetadataMayNotKnowEveryTarget()
    {
        var package = Package("talxis_warehouseitem");
        package.Schema.FindEntity("talxis_warehouseitem")!.AddField("talxis_itemid", CmtFieldTypes.EntityReference, lookupType: "ntg_packagematerial|talxis_product");
        var shell = new EntityMetadata { LogicalName = "talxis_warehouseitem" };
        shell.AddAttribute(new StringAttributeMetadata { LogicalName = "name" });
        shell.AddAttribute(new LookupAttributeMetadata { LogicalName = "talxis_itemid" });
        var metadata = Metadata(shell);
        metadata.AddRelationship(ManyToOne("talxis_warehouseitem", "talxis_itemid", "ntg_packagematerial"));

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, metadata), r => r.Code == ValidationDiagnostics.CmtLookupTargetNotAllowed);

        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("'talxis_product'", finding.Message);
        Assert.Contains("the metadata only knows", finding.Message);
        Assert.Contains("only partly in the metadata", finding.Message);
        Assert.DoesNotContain("Dataverse rejects", finding.Message);
    }

    [Fact]
    public void ManyToOneEntryWithoutItsColumns_SaysWhatIsMissing()
    {
        var package = Package("talxis_contract");
        package.Schema.FindEntity("talxis_contract")!.AddRelationship(new CmtSchemaRelationship { Name = "talxis_contract_talxis_regionid_talxis_region" });
        var metadata = Metadata(Table("talxis_contract"));
        metadata.AddRelationship(ManyToOne("talxis_contract", "talxis_regionid", "talxis_region"));

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, metadata), r => r.Code == ValidationDiagnostics.CmtRelationshipNotInMetadata);

        Assert.DoesNotContain("''", finding.Message);
        Assert.Contains("does not say which column and table", finding.Message);
    }

    [Fact]
    public void ManyToOneEntryOnAnotherTable_SaysWhereItBelongs()
    {
        var package = Package("account");
        package.Schema.FindEntity("account")!.AddRelationship(new CmtSchemaRelationship
        {
            Name = "talxis_contract_talxis_regionid_talxis_region", ReferencingEntity = "talxis_contract", ReferencingAttribute = "talxis_regionid",
            ReferencedEntity = "talxis_region", ReferencedAttribute = "talxis_regionid"
        });
        var metadata = Metadata(Table("account"));
        metadata.AddRelationship(ManyToOne("talxis_contract", "talxis_regionid", "talxis_region"));

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, metadata), r => r.Code == ValidationDiagnostics.CmtRelationshipNotInMetadata);

        Assert.Contains("belongs to table 'talxis_contract'", finding.Message);
    }

    [Fact]
    public void ManyToOneEntryOnATableOnlyPartlyInTheMetadata_SaysTheMetadataMayLeaveItOut()
    {
        var package = Package("talxis_warehouseitem");
        package.Schema.FindEntity("talxis_warehouseitem")!.AddRelationship(new CmtSchemaRelationship
        {
            Name = "talxis_product_talxis_warehouseitem_itemid", ReferencingEntity = "talxis_warehouseitem", ReferencingAttribute = "talxis_itemid",
            ReferencedEntity = "talxis_product", ReferencedAttribute = "talxis_productid"
        });
        var shell = new EntityMetadata { LogicalName = "talxis_warehouseitem" };

        var finding = Assert.Single(new CmtMetadataValidator().Validate(package, Metadata(shell)), r => r.Code == ValidationDiagnostics.CmtRelationshipNotInMetadata);

        Assert.Contains("only partly in the metadata", finding.Message);
    }

    [Theory]
    [InlineData(AttributeSourceType.Calculated, "talxis_total", true)]
    [InlineData(AttributeSourceType.Rollup, "talxis_total", true)]
    [InlineData(AttributeSourceType.Formula, "talxis_total", true)]
    [InlineData(AttributeSourceType.Simple, "talxis_total", false)]
    [InlineData(AttributeSourceType.Simple, "talxis_total_base", true)]
    public void ColumnWhoseValueDataverseComputes_IsAWarningEvenWhenItsFlagsAllowWriting(AttributeSourceType sourceType, string name, bool reported)
    {
        var column = new MoneyAttributeMetadata { LogicalName = name, SourceType = sourceType, IsValidForCreate = true, IsValidForUpdate = true };

        var findings = ValidateField(column, CmtFieldTypes.Money);

        Assert.Equal(reported, findings.Any(r => r.Code == ValidationDiagnostics.CmtFieldNotWritable && r.Severity == ValidationSeverity.Warning));
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
        return new CmtPackage(schema, new CmtData());
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
