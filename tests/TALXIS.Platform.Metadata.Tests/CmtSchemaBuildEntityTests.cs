using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtSchemaBuildEntityTests
{
    private static EntityMetadata Contact()
    {
        var entity = new EntityMetadata
        {
            LogicalName = "contact",
            DisplayName = new Label("Contact"),
            PrimaryIdAttribute = "contactid",
            PrimaryNameAttribute = "fullname",
            ObjectTypeCode = 2,
        };
        entity.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = "contactid", DisplayName = new Label("Contact") });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "fullname", DisplayName = new Label("Full Name") });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "new_nickname", IsCustomAttribute = true });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "lastname", RequiredLevel = RequiredLevel.ApplicationRequired });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "jobtitle" });
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "parentcustomerid", LookupKind = LookupKind.Customer, Targets = new[] { "account", "contact" } });
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "new_projectid", IsCustomAttribute = true });
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "ownerid", LookupKind = LookupKind.Owner, Targets = new[] { "systemuser", "team" } });
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "createdby", IsValidForCreate = false, IsValidForUpdate = false });
        entity.AddAttribute(new PicklistAttributeMetadata { LogicalName = "preferredcontactmethodcode" });
        entity.AddAttribute(new StateAttributeMetadata { LogicalName = "statecode" });
        entity.AddAttribute(new DateTimeAttributeMetadata { LogicalName = "createdon", IsValidForCreate = false, IsValidForUpdate = false });
        entity.AddAttribute(new DateTimeAttributeMetadata { LogicalName = "overriddencreatedon", IsValidForUpdate = false });
        entity.AddAttribute(new BigIntAttributeMetadata { LogicalName = "versionnumber" });
        entity.AddAttribute(new MoneyAttributeMetadata { LogicalName = "creditlimit" });
        entity.AddAttribute(new MoneyAttributeMetadata { LogicalName = "creditlimit_base", IsValidForCreate = false, IsValidForUpdate = false });
        entity.AddAttribute(new IntegerAttributeMetadata { LogicalName = "new_total", IsCustomAttribute = true, SourceType = AttributeSourceType.Rollup });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "new_readonly", IsCustomAttribute = true, IsValidForRead = false });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "parentcustomeridname", AttributeOf = "parentcustomerid" });
        return entity;
    }

    private static readonly RelationshipMetadata[] Relationships =
    {
        new OneToManyRelationshipMetadata { SchemaName = "new_project_contact", ReferencedEntity = "new_Project", ReferencedAttribute = "new_ProjectId", ReferencingEntity = "Contact", ReferencingAttribute = "new_ProjectId" },
        new OneToManyRelationshipMetadata { SchemaName = "contact_customer_accounts", ReferencedEntity = "Account", ReferencedAttribute = "AccountId", ReferencingEntity = "Contact", ReferencingAttribute = "ParentCustomerId" },
        new ManyToManyRelationshipMetadata { SchemaName = "new_contact_tag", Entity1LogicalName = "contact", Entity2LogicalName = "new_tag", IntersectEntityName = "new_contact_tag" },
        new ManyToManyRelationshipMetadata { SchemaName = "new_contact_contact", Entity1LogicalName = "contact", Entity2LogicalName = "contact", IntersectEntityName = "new_contact_contact" },
    };

    private static CmtSchemaEntity Build(CmtFieldSelection selection, ICollection<string>? warnings = null, CmtDataSchema? target = null, bool manyToMany = false) =>
        CmtSchemaBuilder.BuildEntity(Contact(), Relationships, new CmtSchemaBuildOptions { FieldSelection = selection, IncludeManyToMany = manyToMany }, warnings ?? new List<string>(), target);

    [Fact]
    public void Minimal_TakesKeyNameCustomAndRequiredColumns()
    {
        var entity = Build(CmtFieldSelection.Minimal);

        Assert.Equal(new[] { "contactid", "fullname", "new_nickname", "lastname", "new_projectid" }, entity.Fields.Select(f => f.Name));
    }

    [Fact]
    public void Standard_AddsLookupsChoicesAndOverriddenCreatedOn()
    {
        var entity = Build(CmtFieldSelection.Standard);

        Assert.Equal(new[] { "contactid", "fullname", "new_nickname", "lastname", "parentcustomerid", "new_projectid", "preferredcontactmethodcode", "overriddencreatedon" },
            entity.Fields.Select(f => f.Name));
    }

    [Fact]
    public void Full_TakesWritableColumnsPlusAuditUsers()
    {
        var names = Build(CmtFieldSelection.Full).Fields.Select(f => f.Name).ToList();

        Assert.Equal(new[] { "contactid", "fullname", "new_nickname", "lastname", "jobtitle", "parentcustomerid", "new_projectid", "ownerid", "createdby", "preferredcontactmethodcode", "statecode", "overriddencreatedon", "creditlimit" }, names);
    }

    [Theory]
    [InlineData(CmtFieldSelection.Minimal)]
    [InlineData(CmtFieldSelection.Standard)]
    [InlineData(CmtFieldSelection.Full)]
    public void NeverIncludesColumnsCmtCannotMigrate(CmtFieldSelection selection)
    {
        var names = Build(selection).Fields.Select(f => f.Name).ToList();

        foreach (var excluded in new[] { "createdon", "versionnumber", "creditlimit_base", "new_total", "new_readonly", "parentcustomeridname" }) Assert.DoesNotContain(excluded, names);
    }

    [Fact]
    public void MapsTypesLookupTargetsAndFlags()
    {
        var entity = Build(CmtFieldSelection.Full);

        Assert.Equal("Contact", entity.DisplayName);
        Assert.Equal(2, entity.ObjectTypeCode);
        Assert.Equal("contactid", entity.PrimaryIdField);
        Assert.False(entity.DisablePlugins);

        var key = entity.FindField("contactid")!;
        Assert.Equal(CmtFieldTypes.Guid, key.Type);
        Assert.True(key.IsPrimaryKey);
        Assert.False(key.IsUpdateCompare);
        Assert.True(entity.FindField("fullname")!.IsUpdateCompare);

        Assert.Equal(CmtFieldTypes.EntityReference, entity.FindField("parentcustomerid")!.Type);
        Assert.Equal("account|contact", entity.FindField("parentcustomerid")!.LookupType);
        Assert.Equal("new_project", entity.FindField("new_projectid")!.LookupType);
        Assert.True(entity.FindField("new_projectid")!.IsCustomField);
        Assert.Equal(CmtFieldTypes.Owner, entity.FindField("ownerid")!.Type);
        Assert.Null(entity.FindField("ownerid")!.LookupType);
        Assert.Equal(CmtFieldTypes.OptionSetValue, entity.FindField("preferredcontactmethodcode")!.Type);
        Assert.Equal("Full Name", entity.FindField("fullname")!.DisplayName);
    }

    [Fact]
    public void UpdateCompareFallsBackToPrimaryIdWithoutPrimaryName()
    {
        var metadata = Contact();
        metadata.PrimaryNameAttribute = null;

        var entity = CmtSchemaBuilder.BuildEntity(metadata, Relationships, new CmtSchemaBuildOptions { FieldSelection = CmtFieldSelection.Minimal }, new List<string>());

        Assert.True(entity.FindField("contactid")!.IsUpdateCompare);
    }

    [Fact]
    public void ManyToOneEntriesOnlyForDeclaredParents()
    {
        var target = new CmtDataSchema();
        target.AddEntity("new_project", "new_projectid", "new_name", "Project");
        var warnings = new List<string>();

        var entity = Build(CmtFieldSelection.Standard, warnings, target);

        var relationship = Assert.Single(entity.Relationships);
        Assert.Equal("new_project_contact", relationship.Name);
        Assert.Equal("contact", relationship.ReferencingEntity);
        Assert.Equal("new_projectid", relationship.ReferencingAttribute);
        Assert.Equal("new_project", relationship.ReferencedEntity);
        Assert.Contains(warnings, w => w.Contains("'account'"));
        Assert.NotNull(entity.FindField("parentcustomerid"));
    }

    [Fact]
    public void NoRelationshipEntriesWithoutTarget()
    {
        var warnings = new List<string>();

        Assert.Empty(Build(CmtFieldSelection.Standard, warnings).Relationships);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ManyToManyOnlyWithOptionAndDeclaredOtherSide()
    {
        var target = new CmtDataSchema();
        target.AddEntity("new_tag", "new_tagid", "new_name", "Tag");

        Assert.DoesNotContain(Build(CmtFieldSelection.Minimal, target: target).Relationships, r => r.IsManyToMany);

        var entity = Build(CmtFieldSelection.Minimal, target: target, manyToMany: true);
        var tag = Assert.Single(entity.Relationships, r => r.Name == "new_contact_tag");
        Assert.True(tag.IsManyToMany);
        Assert.Equal("new_tag", tag.M2mTargetEntity);
        Assert.Equal("new_tagid", tag.M2mTargetEntityPrimaryKey);
        Assert.Equal("new_contact_tag", tag.RelatedEntityName);
        var self = Assert.Single(entity.Relationships, r => r.Name == "new_contact_contact");
        Assert.True(self.IsReflexive);
        Assert.Equal("contactid", self.M2mTargetEntityPrimaryKey);
    }

    [Fact]
    public void ManyToManyToTableOutsidePackageIsKeptWithWarning()
    {
        var warnings = new List<string>();

        var entity = Build(CmtFieldSelection.Minimal, warnings, new CmtDataSchema(), manyToMany: true);

        var tag = Assert.Single(entity.Relationships, r => r.Name == "new_contact_tag");
        Assert.Equal("new_tag", tag.M2mTargetEntity);
        Assert.Equal("new_tagid", tag.M2mTargetEntityPrimaryKey);
        Assert.Contains(warnings, w => w.Contains("'new_tag'") && w.Contains("must already exist"));
    }

    [Fact]
    public void ManyToManyKeyOfTableOutsidePackageComesFromItsMetadata()
    {
        var tagMetadata = new EntityMetadata { LogicalName = "new_tag", PrimaryIdAttribute = "new_TagKey" };

        var entity = CmtSchemaBuilder.BuildEntity(Contact(), Relationships, new CmtSchemaBuildOptions { IncludeManyToMany = true }, new List<string>(),
            findEntity: name => name == "new_tag" ? tagMetadata : null);

        Assert.Equal("new_tagkey", Assert.Single(entity.Relationships, r => r.Name == "new_contact_tag").M2mTargetEntityPrimaryKey);
    }

    [Fact]
    public void WorkspaceOverloadBuildsByNameAndThrowsWhenAbsent()
    {
        var workspace = new XmlWorkspaceReader().Load(Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace"));
        var name = workspace.Entities[0].LogicalName;

        var entity = CmtWorkspaceSchemaBuilder.BuildEntity(workspace, name, new CmtSchemaBuildOptions(), new List<string>());

        Assert.Equal(name, entity.Name);
        Assert.NotEmpty(entity.Fields);
        Assert.Throws<InvalidOperationException>(() => CmtWorkspaceSchemaBuilder.BuildEntity(workspace, "missing_entity", new CmtSchemaBuildOptions(), new List<string>()));
    }

    [Fact]
    public void BuiltEntityPassesSchemaValidation()
    {
        var schema = new CmtDataSchema();
        CmtSchemaBuilder.AddOrReplaceEntity(schema, Build(CmtFieldSelection.Full));

        var findings = new TALXIS.Platform.Metadata.Validation.CmtDataSchemaValidator().Validate(schema);

        Assert.DoesNotContain(findings, f => f.Severity == TALXIS.Platform.Metadata.Validation.ValidationSeverity.Error);
    }
}
