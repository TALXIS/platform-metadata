using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtSchemaBuilderTests : IDisposable
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", "exhaustive");
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cmt-builder-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ---- BuildEntity ----

    public static TheoryData<CmtFieldSelection, string[]> Selections => new()
    {
        { CmtFieldSelection.Minimal, new[] { "contactid", "fullname", "new_nickname", "lastname", "new_projectid" } },
        { CmtFieldSelection.Standard, new[] { "contactid", "fullname", "new_nickname", "lastname", "parentcustomerid", "new_projectid", "preferredcontactmethodcode", "overriddencreatedon" } },
        { CmtFieldSelection.Full, new[] { "contactid", "fullname", "new_nickname", "lastname", "jobtitle", "parentcustomerid", "new_projectid", "ownerid", "createdby", "preferredcontactmethodcode", "statecode", "overriddencreatedon", "creditlimit" } },
    };

    [Theory]
    [InlineData(CmtFieldSelection.Minimal, true, true)]
    [InlineData(CmtFieldSelection.Minimal, false, false)]
    [InlineData(CmtFieldSelection.Standard, true, true)]
    [InlineData(CmtFieldSelection.Standard, false, true)]
    [InlineData(CmtFieldSelection.Full, true, true)]
    public void BuildEntity_TakesTheCurrencyAlongWithMoney(CmtFieldSelection selection, bool hasCustomMoney, bool expectCurrency)
    {
        var priceList = new EntityMetadata { LogicalName = "new_pricelist", PrimaryIdAttribute = "new_pricelistid", PrimaryNameAttribute = "new_name" };
        priceList.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = "new_pricelistid" });
        priceList.AddAttribute(new StringAttributeMetadata { LogicalName = "new_name" });
        priceList.AddAttribute(new LookupAttributeMetadata { LogicalName = "transactioncurrencyid", Targets = new[] { "transactioncurrency" } });
        if (hasCustomMoney) priceList.AddAttribute(new MoneyAttributeMetadata { LogicalName = "new_amount", IsCustomAttribute = true });

        var entity = CmtSchemaBuilder.BuildEntity(priceList, Array.Empty<RelationshipMetadata>(), new CmtSchemaBuildOptions { FieldSelection = selection }, new List<string>());

        Assert.Equal(expectCurrency, entity.FindField("transactioncurrencyid") != null);
        if (expectCurrency) Assert.Equal("transactioncurrency", entity.FindField("transactioncurrencyid")!.LookupType);
    }

    // Never selected: createdon, versionnumber, the _base money column, the rollup, the unreadable and the derived column.
    [Theory]
    [MemberData(nameof(Selections))]
    public void BuildEntity_SelectsColumns(CmtFieldSelection selection, string[] expected)
    {
        var entity = CmtSchemaBuilder.BuildEntity(Contact(), ContactRelationships, new CmtSchemaBuildOptions { FieldSelection = selection }, new List<string>());

        Assert.Equal(expected, entity.Fields.Select(f => f.Name));
    }

    [Fact]
    public void BuildEntity_MapsTypesLookupTargetsAndUpdateCompare()
    {
        var entity = CmtSchemaBuilder.BuildEntity(Contact(), ContactRelationships, new CmtSchemaBuildOptions { FieldSelection = CmtFieldSelection.Full }, new List<string>());
        var withoutName = Contact();
        withoutName.PrimaryNameAttribute = null;
        var byId = CmtSchemaBuilder.BuildEntity(withoutName, ContactRelationships, new CmtSchemaBuildOptions(), new List<string>());

        Assert.Equal("Contact", entity.DisplayName);
        Assert.Equal(2, entity.ObjectTypeCode);
        Assert.False(entity.DisablePlugins);
        Assert.True(entity.FindField("contactid")!.IsPrimaryKey);
        Assert.False(entity.FindField("contactid")!.IsUpdateCompare);
        Assert.True(entity.FindField("fullname")!.IsUpdateCompare);
        Assert.Equal("Full Name", entity.FindField("fullname")!.DisplayName);
        Assert.Equal("account|contact", entity.FindField("parentcustomerid")!.LookupType);
        Assert.Equal("new_project", entity.FindField("new_projectid")!.LookupType);
        Assert.Equal(CmtFieldTypes.Owner, entity.FindField("ownerid")!.Type);
        Assert.Null(entity.FindField("ownerid")!.LookupType);
        Assert.Equal(CmtFieldTypes.OptionSetValue, entity.FindField("preferredcontactmethodcode")!.Type);
        Assert.True(byId.FindField("contactid")!.IsUpdateCompare);
    }

    [Fact]
    public void BuildEntity_LeavesOutColumnsCmtCannotImportWithWarning()
    {
        var metadata = Contact();
        metadata.AddAttribute(new BigIntAttributeMetadata { LogicalName = "new_bignumber", IsCustomAttribute = true });
        metadata.AddAttribute(new FileAttributeMetadata { LogicalName = "new_document", IsCustomAttribute = true });
        metadata.AddAttribute(new CalendarRulesColumn { LogicalName = "new_rules", IsCustomAttribute = true });
        var warnings = new List<string>();

        var entity = CmtSchemaBuilder.BuildEntity(metadata, ContactRelationships, new CmtSchemaBuildOptions(), warnings);

        Assert.DoesNotContain(entity.Fields, f => f.Name is "new_bignumber" or "new_document" or "new_rules");
        Assert.Contains(warnings, w => w.Contains("'contact.new_bignumber'") && w.Contains("bigint"));
        Assert.Contains(warnings, w => w.Contains("'contact.new_document'") && w.Contains("file column"));
        Assert.Contains(warnings, w => w.Contains("'contact.new_rules'") && w.Contains("'CalendarRules'"));
    }

    [Fact]
    public void BuildEntity_EmitsRelationshipsOnlyWhereCmtWould()
    {
        var target = new CmtDataSchema();
        target.AddEntity("new_project", "new_projectid", "new_name", "Project");
        var warnings = new List<string>();
        var options = new CmtSchemaBuildOptions { IncludeManyToMany = true };

        var entity = CmtSchemaBuilder.BuildEntity(Contact(), ContactRelationships, options, warnings, target,
            name => name == "new_tag" ? new EntityMetadata { LogicalName = "new_tag", PrimaryIdAttribute = "new_TagId" } : null);
        var withoutTarget = CmtSchemaBuilder.BuildEntity(Contact(), ContactRelationships, new CmtSchemaBuildOptions(), new List<string>());

        var manyToOne = Assert.Single(entity.Relationships, r => !r.IsManyToMany);
        Assert.Equal(("new_project_contact", "contact", "new_projectid", "new_project"), (manyToOne.Name, manyToOne.ReferencingEntity, manyToOne.ReferencingAttribute, manyToOne.ReferencedEntity));
        Assert.Contains(warnings, w => w.Contains("'contact.parentcustomerid' points to 'account'"));
        // Each N:N once, from its Entity1 side; new_list_contact belongs to new_list.
        Assert.Equal(new[] { "new_contact_tag", "new_contact_contact" }, entity.Relationships.Where(r => r.IsManyToMany).Select(r => r.Name));
        var tag = entity.Relationships.Single(r => r.Name == "new_contact_tag");
        Assert.Equal(("new_contact_tag", "new_tag", "new_tagid", false), (tag.RelatedEntityName, tag.M2mTargetEntity, tag.M2mTargetEntityPrimaryKey, tag.IsReflexive));
        Assert.Contains(warnings, w => w.Contains("'new_tag'") && w.Contains("must already exist"));
        var self = entity.Relationships.Single(r => r.Name == "new_contact_contact");
        Assert.Equal(("contact", "contactid", true), (self.M2mTargetEntity, self.M2mTargetEntityPrimaryKey, self.IsReflexive));
        Assert.Empty(withoutTarget.Relationships);
    }

    // The lab tables behind the exhaustive fixture, built from their Dataverse metadata, against the real CMT schema.
    [Fact]
    public void BuildEntity_ForLabTables_MatchesTheExhaustiveFixture()
    {
        var fixture = new CmtPackageXmlReader().LoadDirectory(FixturePath).Schema;
        var (parent, child, relationships) = LabTables();
        var warnings = new List<string>();
        var options = new CmtSchemaBuildOptions { IncludeManyToMany = true };

        var builtParent = CmtSchemaBuilder.BuildEntity(parent, relationships, options, warnings, fixture);
        var builtChild = CmtSchemaBuilder.BuildEntity(child, relationships, options, warnings, fixture);

        foreach (var (built, expected) in new[] { (builtParent, fixture.FindEntity("cmtl_parent")!), (builtChild, fixture.FindEntity("cmtl_child")!) })
        {
            Assert.Equal((expected.DisplayName, expected.PrimaryIdField, expected.PrimaryNameField), (built.DisplayName, built.PrimaryIdField, built.PrimaryNameField));
            foreach (var field in built.Fields.Where(f => expected.FindField(f.Name) != null))
            {
                var cmt = expected.FindField(field.Name)!;
                Assert.Equal((cmt.Type, cmt.IsPrimaryKey, cmt.IsUpdateCompare, cmt.IsCustomField), (field.Type, field.IsPrimaryKey, field.IsUpdateCompare, field.IsCustomField));
                Assert.Equal(cmt.LookupType?.Split('|').OrderBy(t => t), field.LookupType?.Split('|').OrderBy(t => t));
            }
        }

        // Standard leaves out ownership and state and adds overriddencreatedon; CMT export refuses the file column.
        Assert.Equal(new[] { "cmtl_file", "ownerid", "statecode", "statuscode" }, fixture.FindEntity("cmtl_child")!.Fields.Select(f => f.Name).Except(builtChild.Fields.Select(f => f.Name)).OrderBy(n => n));
        Assert.Equal(new[] { "overriddencreatedon" }, builtChild.Fields.Select(f => f.Name).Except(fixture.FindEntity("cmtl_child")!.Fields.Select(f => f.Name)));
        var m2m = Assert.Single(builtParent.Relationships);
        var cmtM2m = Assert.Single(fixture.FindEntity("cmtl_parent")!.Relationships);
        Assert.Equal((cmtM2m.Name, cmtM2m.IsManyToMany, cmtM2m.IsReflexive, cmtM2m.RelatedEntityName, cmtM2m.M2mTargetEntity, cmtM2m.M2mTargetEntityPrimaryKey),
            (m2m.Name, m2m.IsManyToMany, m2m.IsReflexive, m2m.RelatedEntityName, m2m.M2mTargetEntity, m2m.M2mTargetEntityPrimaryKey));
        Assert.DoesNotContain(builtChild.Relationships, r => r.IsManyToMany);
        Assert.Contains(warnings, w => w.Contains("'cmtl_child.cmtl_bigint'"));

        var schema = new CmtDataSchema();
        CmtSchemaBuilder.AddOrReplaceEntity(schema, builtParent);
        CmtSchemaBuilder.AddOrReplaceEntity(schema, builtChild);
        Assert.DoesNotContain(new CmtDataSchemaValidator().Validate(schema), r => r.Severity == ValidationSeverity.Error);
        var path = Path.Combine(Directory.CreateDirectory(_root).FullName, "data_schema.xml");
        new CmtPackageXmlWriter().SaveSchema(new CmtPackage(schema), path);
        Assert.Contains("<relationship name=\"cmtl_cmtl_parent_cmtl_child\" manyToMany=\"true\" isreflexive=\"false\" relatedEntityName=\"cmtl_cmtl_parent_cmtl_child\" m2mTargetEntity=\"cmtl_child\" m2mTargetEntityPrimaryKey=\"cmtl_childid\" />",
            File.ReadAllText(path));
    }

    [Fact]
    public void BuildEntity_FromWorkspace_BuildsByNameAndThrowsWhenAbsent()
    {
        var workspace = new XmlWorkspaceReader().Load(Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace"));
        var name = workspace.Entities[0].LogicalName;

        var entity = CmtWorkspaceSchemaBuilder.BuildEntity(workspace, name, new CmtSchemaBuildOptions(), new List<string>());

        Assert.Equal(name, entity.Name);
        Assert.NotEmpty(entity.Fields);
        Assert.Throws<InvalidOperationException>(() => CmtWorkspaceSchemaBuilder.BuildEntity(workspace, "missing_entity", new CmtSchemaBuildOptions(), new List<string>()));
    }

    // ---- Add, replace, merge, remove ----

    [Fact]
    public void AddOrReplaceEntity_AddsOrRefreshesKeepingHandEdits()
    {
        var existing = Entity("account");
        existing.AddField("name", CmtFieldTypes.String, updateCompare: true);
        existing.AddField("new_handadded", CmtFieldTypes.String);
        existing.AddRelationship(new CmtSchemaRelationship { Name = "old_relationship", ReferencedEntity = "contact" });
        existing.SkipUpdate = true;
        var schema = Schema(existing);
        schema.EntityImportOrder.Add("account");
        var refreshed = Entity("account");
        refreshed.AddField("name", CmtFieldTypes.String);
        refreshed.AddField("telephone1", CmtFieldTypes.String);
        refreshed.AddRelationship(new CmtSchemaRelationship { Name = "account_primary_contact", ReferencedEntity = "contact" });
        refreshed.DisplayName = "Account";
        var replaced = Schema(Entity("account"));
        replaced.Entities[0].AddField("new_handadded", CmtFieldTypes.String);

        var added = CmtSchemaBuilder.AddOrReplaceEntity(schema, Entity("contact"));
        var result = CmtSchemaBuilder.AddOrReplaceEntity(schema, refreshed);
        CmtSchemaBuilder.AddOrReplaceEntity(replaced, Entity("account"), replaceFields: true);
        CmtSchemaBuilder.AddOrReplaceEntity(replaced, Entity("contact"));

        Assert.Same(added, schema.FindEntity("contact"));
        Assert.Equal(new[] { "account", "contact" }, schema.EntityImportOrder);
        Assert.Same(existing, result);
        Assert.Equal(new[] { "accountid", "name", "new_handadded", "telephone1" }, result.Fields.Select(f => f.Name));
        Assert.True(result.FindField("name")!.IsUpdateCompare);
        Assert.True(result.SkipUpdate);
        Assert.Equal("Account", result.DisplayName);
        Assert.Equal(new[] { "account_primary_contact" }, result.Relationships.Select(r => r.Name));
        Assert.Equal(new[] { "accountid" }, replaced.FindEntity("account")!.Fields.Select(f => f.Name));
        Assert.Empty(replaced.EntityImportOrder);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddOrReplaceEntity_WithTheEntityAlreadyInTheSchema_KeepsItsFieldsAndRelationships(bool replaceFields)
    {
        var schema = Schema(Entity("account"));
        var account = schema.FindEntity("account")!;
        account.AddField("telephone1", CmtFieldTypes.String);
        account.AddRelationship(new CmtSchemaRelationship { Name = "account_primary_contact", ReferencedEntity = "contact" });

        var result = CmtSchemaBuilder.AddOrReplaceEntity(schema, account, replaceFields);

        Assert.Same(account, result);
        Assert.Equal(new[] { "accountid", "telephone1" }, result.Fields.Select(f => f.Name));
        Assert.Equal(new[] { "account_primary_contact" }, result.Relationships.Select(r => r.Name));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MergeEntity_JoinsPackagesWhicheverComesFirst(bool mainFirst)
    {
        var schema = new CmtDataSchema();
        var warnings = new List<string>();
        var packages = mainFirst ? new[] { SecurityTeamFromMain(), SecurityTeamFromSecurityRules() } : new[] { SecurityTeamFromSecurityRules(), SecurityTeamFromMain() };

        foreach (var team in packages)
            CmtSchemaBuilder.MergeEntity(schema, team, warnings);

        var merged = Assert.Single(schema.Entities);
        Assert.Equal(2, merged.Relationships.Count);
        Assert.Equal(4, merged.Fields.Count);
        Assert.Equal(!mainFirst, merged.SkipUpdate);
        Assert.Contains("skipupdate", Assert.Single(warnings));
    }

    [Fact]
    public void RemoveEntity_DropsEntityOrderEntryAndRelationshipsPointingAtIt()
    {
        var contact = Entity("contact", "account");
        contact.AddRelationship(new CmtSchemaRelationship { Name = "contact_customer_accounts", ReferencedEntity = "account" });
        var tag = Entity("new_tag");
        tag.AddRelationship(new CmtSchemaRelationship { Name = "new_tag_account", IsManyToMany = true, M2mTargetEntity = "account" });
        tag.AddRelationship(new CmtSchemaRelationship { Name = "new_tag_contact", IsManyToMany = true, M2mTargetEntity = "contact" });
        var schema = Schema(Entity("account"), contact, tag);
        foreach (var name in new[] { "account", "contact", "new_tag" })
            schema.EntityImportOrder.Add(name);

        Assert.True(CmtSchemaBuilder.RemoveEntity(schema, "account"));
        Assert.False(CmtSchemaBuilder.RemoveEntity(schema, "account"));

        Assert.Null(schema.FindEntity("account"));
        Assert.Equal(new[] { "contact", "new_tag" }, schema.EntityImportOrder);
        Assert.Empty(contact.Relationships);
        Assert.Equal(new[] { "new_tag_contact" }, tag.Relationships.Select(r => r.Name));
        Assert.NotNull(contact.FindField("lookup0"));
    }

    // ---- ResolveImportOrder ----

    // Entities are "name" or "name:target,target"; a target looks up through an entityreference field (a|b is polymorphic),
    // "@target" through an N:1 relationship entry. Orders are space-separated; a warning of null means none.
    public static TheoryData<string, string?, string?, string, string?> ImportOrderCases => new()
    {
        // chain
        { "task:project project:account account", null, null, "account project task", null },
        // diamond
        { "d:b,c b:a c:a a", null, null, "a b c d", null },
        // self-reference and undeclared parent
        { "account:account,systemuser", null, null, "account", null },
        // N:1 relationship entries and polymorphic lookups
        { "note:contact|account contact:@account account", null, null, "account contact note", null },
        // two-node cycle, broken at the current order
        { "account:contact contact:account", null, null, "account contact", "'account' and 'contact' look each other up" },
        // a hand-written order is kept unless a lookup disagrees
        { "x y z", "z y x", null, "z y x", null },
        { "account contact:account", "contact account", null, "account contact", null },
        // manual order wins over a lookup
        { "account contact:account", null, "contact account", "contact account", "'contact' is imported before 'account'" },
        // a partial manual order moves only the listed entities; the rest follows lookups
        { "task:auth auth perm parent child:parent", null, "perm auth", "perm auth task parent child", null },
        { "contact:account account", null, "contact", "account contact", null },
        { "task:project other account project", null, "project account", "other project task account", null },
        // a manual name the schema does not declare is ignored
        { "account", null, "acount account", "account", "'acount'" },
    };

    [Theory]
    [MemberData(nameof(ImportOrderCases))]
    public void ResolveImportOrder_OrdersParentsFirst(string entities, string? currentOrder, string? manualOrder, string expected, string? warning)
    {
        var schema = Schema(entities.Split(' ').Select(ParseEntity).ToArray());
        foreach (var name in currentOrder?.Split(' ') ?? Array.Empty<string>())
            schema.EntityImportOrder.Add(name);
        var warnings = new List<string>();

        CmtSchemaBuilder.ResolveImportOrder(schema, warnings, manualOrder?.Split(' '));

        Assert.Equal(expected.Split(' '), schema.EntityImportOrder);
        Assert.Equal(expected.Split(' '), schema.Entities.Select(e => e.Name));
        if (warning == null)
            Assert.Empty(warnings);
        else
            Assert.Contains(warning, Assert.Single(warnings));
    }

    [Fact]
    public void ResolveImportOrder_OnLoadedPackageMovesOnlyTheEntityBlocks()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CmtPackageLayout.SchemaFileName);
        File.Copy(Path.Combine(FixturePath, CmtPackageLayout.SchemaFileName), path);
        var package = new CmtPackageXmlReader().Load(path);

        // account and contact look each other up; the manual order puts contact first.
        CmtSchemaBuilder.ResolveImportOrder(package.Schema, new List<string>(), manualOrder: new[] { "contact", "account" });
        new CmtPackageXmlWriter().SaveSchema(package, path);

        var original = XDocument.Load(Path.Combine(FixturePath, CmtPackageLayout.SchemaFileName)).Root!;
        var saved = XDocument.Load(path).Root!;
        var expected = new[] { "contact", "account", "cmtl_parent", "cmtl_child", "appointment" };
        Assert.Equal(expected, saved.Elements("entity").Select(e => (string)e.Attribute("name")!));
        Assert.Equal(expected, saved.Element("entityImportOrder")!.Elements("entityName").Select(e => e.Value));
        foreach (var entity in original.Elements("entity"))
        {
            var name = (string)entity.Attribute("name")!;
            Assert.True(XNode.DeepEquals(entity, saved.Elements("entity").Single(e => (string)e.Attribute("name")! == name)), $"entity '{name}' changed");
        }
    }

    // ---- Helpers ----

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

    // Relationship files carry schema names, so the casing differs from the logical names on purpose.
    private static readonly RelationshipMetadata[] ContactRelationships =
    {
        new OneToManyRelationshipMetadata { SchemaName = "new_project_contact", ReferencedEntity = "new_Project", ReferencedAttribute = "new_ProjectId", ReferencingEntity = "Contact", ReferencingAttribute = "new_ProjectId" },
        new OneToManyRelationshipMetadata { SchemaName = "contact_customer_accounts", ReferencedEntity = "Account", ReferencedAttribute = "AccountId", ReferencingEntity = "Contact", ReferencingAttribute = "ParentCustomerId" },
        new ManyToManyRelationshipMetadata { SchemaName = "new_contact_tag", Entity1LogicalName = "contact", Entity2LogicalName = "new_tag", IntersectEntityName = "new_contact_tag" },
        new ManyToManyRelationshipMetadata { SchemaName = "new_contact_contact", Entity1LogicalName = "contact", Entity2LogicalName = "contact", IntersectEntityName = "new_contact_contact" },
        new ManyToManyRelationshipMetadata { SchemaName = "new_list_contact_association", Entity1LogicalName = "new_list", Entity2LogicalName = "contact", IntersectEntityName = "new_list_contact" },
    };

    // cmtl_parent and cmtl_child as Dataverse describes them (prokopus lab), trimmed to the columns that decide the outcome.
    private static (EntityMetadata Parent, EntityMetadata Child, RelationshipMetadata[] Relationships) LabTables()
    {
        var parent = LabTable("cmtl_parent", "CMTL Parent");
        var child = LabTable("cmtl_child", "CMTL Child");
        foreach (var name in new[] { "cmtl_string", "cmtl_memo" })
            child.AddAttribute(new StringAttributeMetadata { LogicalName = name, IsCustomAttribute = true });
        child.AddAttribute(new IntegerAttributeMetadata { LogicalName = "cmtl_number", IsCustomAttribute = true });
        child.AddAttribute(new DecimalAttributeMetadata { LogicalName = "cmtl_decimal", IsCustomAttribute = true });
        child.AddAttribute(new DoubleAttributeMetadata { LogicalName = "cmtl_float", IsCustomAttribute = true });
        child.AddAttribute(new MoneyAttributeMetadata { LogicalName = "cmtl_money", IsCustomAttribute = true });
        child.AddAttribute(new MoneyAttributeMetadata { LogicalName = "cmtl_money_base", IsCustomAttribute = true, IsValidForCreate = false, IsValidForUpdate = false });
        child.AddAttribute(new BooleanAttributeMetadata { LogicalName = "cmtl_bool", IsCustomAttribute = true });
        foreach (var name in new[] { "cmtl_datetime", "cmtl_dateonly", "cmtl_tzindependent" })
            child.AddAttribute(new DateTimeAttributeMetadata { LogicalName = name, IsCustomAttribute = true });
        child.AddAttribute(new PicklistAttributeMetadata { LogicalName = "cmtl_choice", IsCustomAttribute = true });
        child.AddAttribute(new MultiSelectPicklistAttributeMetadata { LogicalName = "cmtl_multichoice", IsCustomAttribute = true });
        child.AddAttribute(new LookupAttributeMetadata { LogicalName = "cmtl_parentid", IsCustomAttribute = true, Targets = new[] { "cmtl_parent" } });
        child.AddAttribute(new LookupAttributeMetadata { LogicalName = "cmtl_parentchildid", IsCustomAttribute = true, Targets = new[] { "cmtl_child" } });
        child.AddAttribute(new LookupAttributeMetadata { LogicalName = "cmtl_customerid", IsCustomAttribute = true, LookupKind = LookupKind.Customer, Targets = new[] { "account", "contact" } });
        child.AddAttribute(new LookupAttributeMetadata { LogicalName = "cmtl_regardingid", IsCustomAttribute = true, Targets = new[] { "account", "cmtl_parent", "contact" } });
        child.AddAttribute(new StringAttributeMetadata { LogicalName = "cmtl_customeridname", IsCustomAttribute = true, AttributeOf = "cmtl_customerid" });
        child.AddAttribute(new ImageAttributeMetadata { LogicalName = "cmtl_image", IsCustomAttribute = true, AttributeOf = "cmtl_imageid" });
        child.AddAttribute(new StringAttributeMetadata { LogicalName = "cmtl_image_url", IsCustomAttribute = true, AttributeOf = "cmtl_imageid" });
        child.AddAttribute(new FileAttributeMetadata { LogicalName = "cmtl_file", IsCustomAttribute = true, IsValidForCreate = false, IsValidForUpdate = false });
        child.AddAttribute(new BigIntAttributeMetadata { LogicalName = "cmtl_bigint", IsCustomAttribute = true });

        var relationships = new RelationshipMetadata[]
        {
            new ManyToManyRelationshipMetadata { SchemaName = "cmtl_cmtl_parent_cmtl_child", Entity1LogicalName = "cmtl_parent", Entity2LogicalName = "cmtl_child", IntersectEntityName = "cmtl_cmtl_parent_cmtl_child" },
            new OneToManyRelationshipMetadata { SchemaName = "cmtl_cmtl_child_cmtl_parent_parentid", ReferencedEntity = "cmtl_parent", ReferencedAttribute = "cmtl_parentid", ReferencingEntity = "cmtl_child", ReferencingAttribute = "cmtl_parentid" },
        };
        return (parent, child, relationships);
    }

    private static EntityMetadata LabTable(string name, string displayName)
    {
        var entity = new EntityMetadata { LogicalName = name, DisplayName = new Label(displayName), PrimaryIdAttribute = name + "id", PrimaryNameAttribute = "cmtl_name" };
        entity.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = name + "id" });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "cmtl_name", IsCustomAttribute = true, RequiredLevel = RequiredLevel.ApplicationRequired });
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "ownerid", LookupKind = LookupKind.Owner, RequiredLevel = RequiredLevel.SystemRequired, Targets = new[] { "systemuser", "team" } });
        entity.AddAttribute(new StateAttributeMetadata { LogicalName = "statecode", RequiredLevel = RequiredLevel.SystemRequired });
        entity.AddAttribute(new StatusAttributeMetadata { LogicalName = "statuscode" });
        entity.AddAttribute(new DateTimeAttributeMetadata { LogicalName = "createdon", IsValidForCreate = false, IsValidForUpdate = false });
        entity.AddAttribute(new DateTimeAttributeMetadata { LogicalName = "overriddencreatedon", IsValidForUpdate = false });
        entity.AddAttribute(new BigIntAttributeMetadata { LogicalName = "versionnumber", IsValidForCreate = false, IsValidForUpdate = false });
        return entity;
    }

    private static CmtSchemaEntity SecurityTeamFromMain()
    {
        var team = new CmtSchemaEntity { Name = "talxis_securityteam", DisplayName = "Security Team", PrimaryIdField = "talxis_securityteamid", SkipUpdate = false };
        team.AddField("talxis_securityteamid", CmtFieldTypes.Guid, updateCompare: true);
        foreach (var name in new[] { "talxis_name", "talxis_code", "talxis_description" })
            team.AddField(name, CmtFieldTypes.String);
        return team;
    }

    private static CmtSchemaEntity SecurityTeamFromSecurityRules()
    {
        var team = new CmtSchemaEntity { Name = "talxis_securityteam", DisplayName = "Security Team", PrimaryIdField = "talxis_securityteamid", DisablePlugins = false, SkipUpdate = true };
        team.AddField("talxis_securityteamid", CmtFieldTypes.Guid);
        team.AddField("talxis_name", CmtFieldTypes.String);
        team.AddRelationship(new CmtSchemaRelationship { Name = "talxis_t_securityteam_t_config_authruleset", IsManyToMany = true, M2mTargetEntity = "talxis_configuration_authruleset" });
        team.AddRelationship(new CmtSchemaRelationship { Name = "ntg_talxis_product_talxis_securityteam", IsManyToMany = true, M2mTargetEntity = "talxis_product" });
        return team;
    }

    private static CmtSchemaEntity ParseEntity(string spec)
    {
        var parts = spec.Split(':');
        var targets = parts.Length > 1 ? parts[1].Split(',') : Array.Empty<string>();
        var entity = Entity(parts[0], targets.Where(t => !t.StartsWith("@")).ToArray());
        foreach (var target in targets.Where(t => t.StartsWith("@")))
            entity.AddRelationship(new CmtSchemaRelationship { Name = $"{parts[0]}_{target.Substring(1)}", ReferencedEntity = target.Substring(1) });

        return entity;
    }

    private static CmtSchemaEntity Entity(string name, params string[] lookupTargets)
    {
        var entity = new CmtSchemaEntity { Name = name, PrimaryIdField = name + "id" };
        entity.AddField(name + "id", CmtFieldTypes.Guid);
        for (var i = 0; i < lookupTargets.Length; i++)
            entity.AddField($"lookup{i}", CmtFieldTypes.EntityReference, lookupType: lookupTargets[i]);

        return entity;
    }

    private static CmtDataSchema Schema(params CmtSchemaEntity[] entities)
    {
        var schema = new CmtDataSchema();
        foreach (var entity in entities)
            schema.Entities.Add(entity);

        return schema;
    }

    private sealed class CalendarRulesColumn : AttributeMetadata
    {
        public override AttributeType AttributeType => AttributeType.CalendarRules;
    }
}
