using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtSchemaBuilderTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage");

    private static CmtSchemaEntity Entity(string name, params string[] lookupTargets)
    {
        var entity = new CmtSchemaEntity { Name = name, PrimaryIdField = name + "id" };
        entity.AddField(name + "id", CmtFieldTypes.Guid);
        for (var i = 0; i < lookupTargets.Length; i++) entity.AddField($"lookup{i}", CmtFieldTypes.EntityReference, lookupType: lookupTargets[i]);
        return entity;
    }

    private static CmtDataSchema Schema(params CmtSchemaEntity[] entities)
    {
        var schema = new CmtDataSchema();
        foreach (var entity in entities) schema.Entities.Add(entity);
        return schema;
    }

    private static List<string> Resolve(CmtDataSchema schema, List<string>? warnings = null)
    {
        CmtSchemaBuilder.ResolveImportOrder(schema, warnings ?? new List<string>());
        return schema.EntityImportOrder.ToList();
    }

    [Fact]
    public void AddOrReplaceEntity_AddsNewEntityAndExtendsImportOrderInUse()
    {
        var schema = Schema(Entity("account"));
        schema.EntityImportOrder.Add("account");

        var added = CmtSchemaBuilder.AddOrReplaceEntity(schema, Entity("contact"));

        Assert.Same(added, schema.FindEntity("contact"));
        Assert.Equal(new[] { "account", "contact" }, schema.EntityImportOrder);
    }

    [Fact]
    public void AddOrReplaceEntity_DoesNotStartAnImportOrderThatIsNotInUse()
    {
        var schema = Schema(Entity("account"));

        CmtSchemaBuilder.AddOrReplaceEntity(schema, Entity("contact"));

        Assert.Empty(schema.EntityImportOrder);
    }

    [Fact]
    public void AddOrReplaceEntity_KeepsHandEditedFieldsAndAppendsNewOnes()
    {
        var existing = Entity("account");
        existing.AddField("name", CmtFieldTypes.String, updateCompare: true);
        existing.AddField("new_handadded", CmtFieldTypes.String);
        existing.SkipUpdate = true;
        var schema = Schema(existing);

        var refreshed = Entity("account");
        refreshed.AddField("name", CmtFieldTypes.String);
        refreshed.AddField("telephone1", CmtFieldTypes.String);
        refreshed.DisplayName = "Account";

        var result = CmtSchemaBuilder.AddOrReplaceEntity(schema, refreshed);

        Assert.Same(existing, result);
        Assert.Equal(new[] { "accountid", "name", "new_handadded", "telephone1" }, result.Fields.Select(f => f.Name));
        Assert.True(result.FindField("name")!.IsUpdateCompare);
        Assert.True(result.SkipUpdate);
        Assert.Equal("Account", result.DisplayName);
    }

    [Fact]
    public void AddOrReplaceEntity_ReplaceFieldsTakesOnlyTheNewFields()
    {
        var existing = Entity("account");
        existing.AddField("new_handadded", CmtFieldTypes.String);
        var schema = Schema(existing);

        var refreshed = Entity("account");
        refreshed.AddField("telephone1", CmtFieldTypes.String);

        CmtSchemaBuilder.AddOrReplaceEntity(schema, refreshed, replaceFields: true);

        Assert.Equal(new[] { "accountid", "telephone1" }, schema.FindEntity("account")!.Fields.Select(f => f.Name));
    }

    [Fact]
    public void AddOrReplaceEntity_TakesRelationshipsFromTheNewEntity()
    {
        var existing = Entity("contact");
        existing.AddRelationship(new CmtSchemaRelationship { Name = "old_relationship", ReferencedEntity = "account" });
        var schema = Schema(existing);

        var refreshed = Entity("contact");
        refreshed.AddRelationship(new CmtSchemaRelationship { Name = "contact_customer_accounts", ReferencedEntity = "account" });

        CmtSchemaBuilder.AddOrReplaceEntity(schema, refreshed);

        Assert.Equal(new[] { "contact_customer_accounts" }, schema.FindEntity("contact")!.Relationships.Select(r => r.Name));
    }

    private static CmtSchemaEntity SecurityTeamFromMain()
    {
        var team = new CmtSchemaEntity { Name = "talxis_securityteam", DisplayName = "Security Team", PrimaryIdField = "talxis_securityteamid", SkipUpdate = false };
        team.AddField("talxis_securityteamid", CmtFieldTypes.Guid, updateCompare: true);
        foreach (var name in new[] { "talxis_name", "talxis_code", "talxis_description" }) team.AddField(name, CmtFieldTypes.String);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MergeEntity_KeepsRelationshipsWhicheverPackageComesFirst(bool mainFirst)
    {
        var schema = new CmtDataSchema();
        var packages = mainFirst ? new[] { SecurityTeamFromMain(), SecurityTeamFromSecurityRules() } : new[] { SecurityTeamFromSecurityRules(), SecurityTeamFromMain() };

        foreach (var team in packages) CmtSchemaBuilder.MergeEntity(schema, team);

        var merged = Assert.Single(schema.Entities);
        Assert.Equal(2, merged.Relationships.Count);
        Assert.Equal(4, merged.Fields.Count);
    }

    [Fact]
    public void MergeEntity_FirstPackageWinsAttributesAndFieldsAndReportsConflicts()
    {
        var schema = new CmtDataSchema();
        var warnings = new List<string>();

        CmtSchemaBuilder.MergeEntity(schema, SecurityTeamFromMain(), warnings);
        var merged = CmtSchemaBuilder.MergeEntity(schema, SecurityTeamFromSecurityRules(), warnings);

        Assert.False(merged.SkipUpdate);
        Assert.False(merged.DisablePlugins);
        Assert.True(merged.FindField("talxis_securityteamid")!.IsUpdateCompare);
        Assert.Contains("skipupdate", Assert.Single(warnings));
    }

    [Fact]
    public void MergeEntity_AddsEntityThatIsNotThereYet()
    {
        var schema = Schema(Entity("account"));
        schema.EntityImportOrder.Add("account");

        CmtSchemaBuilder.MergeEntity(schema, Entity("contact"));

        Assert.NotNull(schema.FindEntity("contact"));
        Assert.Equal(new[] { "account", "contact" }, schema.EntityImportOrder);
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
        foreach (var name in new[] { "account", "contact", "new_tag" }) schema.EntityImportOrder.Add(name);

        Assert.True(CmtSchemaBuilder.RemoveEntity(schema, "account"));

        Assert.Null(schema.FindEntity("account"));
        Assert.Equal(new[] { "contact", "new_tag" }, schema.EntityImportOrder);
        Assert.Empty(contact.Relationships);
        Assert.Equal(new[] { "new_tag_contact" }, tag.Relationships.Select(r => r.Name));
        Assert.NotNull(contact.FindField("lookup0"));
    }

    [Fact]
    public void RemoveEntity_ReturnsFalseWhenAbsent()
    {
        Assert.False(CmtSchemaBuilder.RemoveEntity(Schema(Entity("account")), "contact"));
    }

    [Fact]
    public void ResolveImportOrder_PutsParentsBeforeChildren()
    {
        var order = Resolve(Schema(Entity("new_task", "new_project"), Entity("new_project", "account"), Entity("account")));

        Assert.Equal(new[] { "account", "new_project", "new_task" }, order);
    }

    [Fact]
    public void ResolveImportOrder_UsesN1RelationshipsAndPolymorphicLookups()
    {
        var contact = Entity("contact");
        contact.AddRelationship(new CmtSchemaRelationship { Name = "contact_customer_accounts", ReferencedEntity = "account" });
        var order = Resolve(Schema(Entity("new_note", "contact|account"), contact, Entity("account")));

        Assert.Equal(new[] { "account", "contact", "new_note" }, order);
    }

    [Fact]
    public void ResolveImportOrder_HandlesDiamondOnce()
    {
        var order = Resolve(Schema(Entity("d", "b", "c"), Entity("b", "a"), Entity("c", "a"), Entity("a")));

        Assert.Equal(new[] { "a", "b", "c", "d" }, order);
    }

    [Fact]
    public void ResolveImportOrder_IgnoresSelfReferencesAndUndeclaredParents()
    {
        var warnings = new List<string>();
        var order = Resolve(Schema(Entity("account", "account", "systemuser")), warnings);

        Assert.Equal(new[] { "account" }, order);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ResolveImportOrder_KeepsHandWrittenOrderWhenNoLookupDisagrees()
    {
        var schema = Schema(Entity("talxis_configuration_authretrievefilter"), Entity("talxis_entityauthtemplate"), Entity("talxis_permissionleveldefaults"));
        foreach (var name in new[] { "talxis_permissionleveldefaults", "talxis_entityauthtemplate", "talxis_configuration_authretrievefilter" }) schema.EntityImportOrder.Add(name);

        var order = Resolve(schema);

        Assert.Equal(new[] { "talxis_permissionleveldefaults", "talxis_entityauthtemplate", "talxis_configuration_authretrievefilter" }, order);
        Assert.Equal(order, schema.Entities.Select(e => e.Name));
    }

    [Fact]
    public void ResolveImportOrder_MovesChildAfterParentEvenAgainstHandWrittenOrder()
    {
        var schema = Schema(Entity("account"), Entity("contact", "account"));
        schema.EntityImportOrder.Add("contact");
        schema.EntityImportOrder.Add("account");

        Assert.Equal(new[] { "account", "contact" }, Resolve(schema));
    }

    [Fact]
    public void ResolveImportOrder_BreaksTwoNodeCycleDeterministicallyAndWarns()
    {
        var warnings = new List<string>();
        var first = Resolve(Schema(Entity("account", "contact"), Entity("contact", "account")), warnings);
        var second = Resolve(Schema(Entity("account", "contact"), Entity("contact", "account")));

        Assert.Equal(new[] { "account", "contact" }, first);
        Assert.Equal(first, second);
        var warning = Assert.Single(warnings);
        Assert.Contains("'contact'", warning);
        Assert.Contains("'account'", warning);
    }

    [Fact]
    public void ResolveImportOrder_ManualOrderWinsOverLookupAndWarns()
    {
        var warnings = new List<string>();
        var schema = Schema(Entity("account"), Entity("contact", "account"));

        CmtSchemaBuilder.ResolveImportOrder(schema, warnings, manualOrder: new[] { "contact", "account" });

        Assert.Equal(new[] { "contact", "account" }, schema.EntityImportOrder);
        var warning = Assert.Single(warnings);
        Assert.Contains("'contact' is imported before 'account'", warning);
    }

    [Fact]
    public void ResolveImportOrder_PartialManualOrderLetsTheRestFollowLookups()
    {
        var warnings = new List<string>();
        var schema = Schema(
            Entity("new_task", "talxis_entityauthtemplate"),
            Entity("talxis_entityauthtemplate"),
            Entity("talxis_permissionleveldefaults"),
            Entity("new_parent"),
            Entity("new_child", "new_parent"));

        CmtSchemaBuilder.ResolveImportOrder(schema, warnings, manualOrder: new[] { "talxis_permissionleveldefaults", "talxis_entityauthtemplate" });

        Assert.Equal(new[] { "talxis_permissionleveldefaults", "talxis_entityauthtemplate", "new_task", "new_parent", "new_child" }, schema.EntityImportOrder);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ResolveImportOrder_UnlistedParentOfManualEntityGoesFirst()
    {
        var schema = Schema(Entity("contact", "account"), Entity("account"));

        CmtSchemaBuilder.ResolveImportOrder(schema, new List<string>(), manualOrder: new[] { "contact" });

        Assert.Equal(new[] { "account", "contact" }, schema.EntityImportOrder);
    }

    [Fact]
    public void ResolveImportOrder_ManualOrderNamingUnknownEntityIsIgnoredWithWarning()
    {
        var warnings = new List<string>();
        var schema = Schema(Entity("account"));

        CmtSchemaBuilder.ResolveImportOrder(schema, warnings, manualOrder: new[] { "acount", "account" });

        Assert.Equal(new[] { "account" }, schema.EntityImportOrder);
        Assert.Contains("'acount'", Assert.Single(warnings));
    }

    [Fact]
    public void ReferencedEntities_ListsRelationshipAndLookupTargetsOnce()
    {
        var entity = Entity("new_note", "account|contact", "account");
        entity.AddRelationship(new CmtSchemaRelationship { Name = "new_note_project", ReferencedEntity = "new_project" });
        entity.AddRelationship(new CmtSchemaRelationship { Name = "new_note_tag", IsManyToMany = true, M2mTargetEntity = "new_tag" });

        Assert.Equal(new[] { "new_project", "account", "contact" }, CmtSchemaBuilder.ReferencedEntities(entity));
    }

    [Fact]
    public void ResolveImportOrder_PartialManualOrderMovesOnlyListedEntities()
    {
        // Portal permission packages: four of seven entities listed; the rule set lookups carry no lookupType, so they are invisible.
        var warnings = new List<string>();
        var schema = Schema(
            Entity("talxis_securityteam"),
            Entity("talxis_configuration_authruleset"),
            Entity("talxis_configuration_authcontextvariable"),
            Entity("talxis_permissionleveldefaults"),
            Entity("talxis_configuration_authretrievefilter"),
            Entity("talxis_configuration_authwritecondition"),
            Entity("talxis_entityauthtemplate"));
        schema.FindEntity("talxis_configuration_authretrievefilter")!.AddField("talxis_authrulesetid", CmtFieldTypes.EntityReference);
        schema.FindEntity("talxis_configuration_authwritecondition")!.AddField("talxis_authrulesetid", CmtFieldTypes.EntityReference);

        CmtSchemaBuilder.ResolveImportOrder(schema, warnings, manualOrder: new[]
        {
            "talxis_permissionleveldefaults", "talxis_entityauthtemplate", "talxis_configuration_authretrievefilter", "talxis_configuration_authwritecondition"
        });

        Assert.Equal(new[]
        {
            "talxis_securityteam",
            "talxis_configuration_authruleset",
            "talxis_configuration_authcontextvariable",
            "talxis_permissionleveldefaults",
            "talxis_entityauthtemplate",
            "talxis_configuration_authretrievefilter",
            "talxis_configuration_authwritecondition",
        }, schema.EntityImportOrder);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ResolveImportOrder_UnlistedChildWaitsForListedParentWithoutMovingOthers()
    {
        var schema = Schema(Entity("new_task", "new_project"), Entity("new_other"), Entity("account"), Entity("new_project"));

        CmtSchemaBuilder.ResolveImportOrder(schema, new List<string>(), manualOrder: new[] { "new_project", "account" });

        Assert.Equal(new[] { "new_other", "new_project", "new_task", "account" }, schema.EntityImportOrder);
    }

    private const string OrderSchema = """
        <entities >
          <entity name="account" displayname="Account" etc="1" primaryidfield="accountid" primarynamefield="name" disableplugins="false">
            <fields>
              <field displayname="Account" name="accountid" type="guid" primaryKey="true" />
              <field displayname="Account Name" name="name" type="string" updateCompare="true" />
              <field displayname="Primary Contact" name="primarycontactid" type="entityreference" lookupType="contact" />
            </fields>
          </entity>
          <entity name="contact" displayname="Contact" etc="2" primaryidfield="contactid" primarynamefield="fullname" disableplugins="false">
            <fields>
              <field displayname="Contact" name="contactid" type="guid" primaryKey="true" />
              <field displayname="Full Name" name="fullname" type="string" updateCompare="true" />
              <field displayname="Company Name" name="parentcustomerid" type="entityreference" lookupType="account|contact" />
            </fields>
          </entity>
          <entity name="new_project" displayname="Project" etc="10001" primaryidfield="new_projectid" primarynamefield="new_name" disableplugins="true">
            <fields>
              <field displayname="Project" name="new_projectid" type="guid" primaryKey="true" />
              <field displayname="Name" name="new_name" type="string" updateCompare="true" customfield="true" />
              <field displayname="Account" name="new_accountid" type="entityreference" lookupType="account" customfield="true" />
            </fields>
          </entity>
          <entityImportOrder>
            <entityName>account</entityName>
            <entityName>contact</entityName>
            <entityName>new_project</entityName>
          </entityImportOrder>
        </entities>
        """;

    [Fact]
    public void ResolveImportOrder_OnLoadedPackageMovesOnlyTheEntityBlocks()
    {
        var schemaPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(schemaPath, OrderSchema);
        var package = new CmtPackageXmlReader().Load(schemaPath);
        var warnings = new List<string>();

        CmtSchemaBuilder.ResolveImportOrder(package.Schema, warnings, manualOrder: new[] { "new_project", "account" });

        var copy = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        File.Copy(schemaPath, copy);
        try
        {
            new CmtPackageXmlWriter().SaveSchema(package, copy);
            var original = XDocument.Load(schemaPath).Root!;
            var saved = XDocument.Load(copy).Root!;

            Assert.Equal(new[] { "new_project", "contact", "account" }, saved.Elements("entity").Select(e => (string)e.Attribute("name")!));
            Assert.Equal(new[] { "new_project", "contact", "account" }, saved.Element("entityImportOrder")!.Elements("entityName").Select(e => e.Value));
            foreach (var entity in original.Elements("entity"))
            {
                var name = (string)entity.Attribute("name")!;
                Assert.True(XNode.DeepEquals(entity, saved.Elements("entity").Single(e => (string)e.Attribute("name")! == name)), $"entity '{name}' changed");
            }

            Assert.Equal(2, warnings.Count);
        }
        finally
        {
            File.Delete(copy);
            File.Delete(schemaPath);
        }
    }
}
