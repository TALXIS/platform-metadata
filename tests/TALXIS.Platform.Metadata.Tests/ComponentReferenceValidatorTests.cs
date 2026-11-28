using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentReferenceValidatorTests : IDisposable
{
    private const string TicketFormId = "{11111111-1111-1111-1111-111111111111}";
    private const string CustomerFormId = "{22222222-2222-2222-2222-222222222222}";
    private const string TicketViewId = "{33333333-3333-3333-3333-333333333333}";
    private const string CustomerViewId = "{44444444-4444-4444-4444-444444444444}";

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"ref-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
    }

    private List<ValidationResult> Findings(string? path = null) =>
        new WorkspaceValidator().ValidateDirectory(path ?? _workspace).Results
            .Where(r => r.Stage == ValidationStage.ComponentReference)
            .ToList();

    private string TicketForm => Path.Combine(_workspace, "Entities", "pba_ticket", "FormXml", "main", $"{TicketFormId}.xml");
    private string CustomerForm => Path.Combine(_workspace, "Entities", "pba_customer", "FormXml", "main", $"{CustomerFormId}.xml");
    private string TicketView => Path.Combine(_workspace, "Entities", "pba_ticket", "SavedQueries", $"{TicketViewId}.xml");
    private string SiteMap => Path.Combine(_workspace, "AppModuleSiteMaps", "pba_app", "AppModuleSiteMap.xml");
    private string AppModule => Path.Combine(_workspace, "AppModules", "pba_app", "AppModule.xml");

    private static void Replace(string file, string oldValue, string newValue)
    {
        var text = File.ReadAllText(file);
        Assert.Contains(oldValue, text);
        File.WriteAllText(file, text.Replace(oldValue, newValue));
    }

    private static ValidationResult Single(List<ValidationResult> findings, string code)
    {
        var finding = Assert.Single(findings);
        Assert.Equal(code, finding.Code);
        Assert.NotNull(finding.FilePath);
        Assert.NotNull(finding.Line);
        Assert.NotNull(finding.Column);
        return finding;
    }

    // ---- Valid workspace ----

    [Fact]
    public void ValidWorkspace_NoFindings()
    {
        Fixture.Write(_workspace);

        Assert.Empty(Findings());
    }

    // ---- Forms ----

    [Fact]
    public void FormControl_UnknownColumn_ReportsErrorWithSuggestionAndLocation()
    {
        Fixture.Write(_workspace);
        Replace(TicketForm, "datafieldname=\"pba_priority\" disabled", "datafieldname=\"pba_priorty\" disabled");

        var finding = Single(Findings(), ValidationDiagnostics.FormControlColumnUnknown);

        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Equal(TicketForm, finding.FilePath);
        Assert.Contains("'pba_priorty'", finding.Message);
        Assert.Contains("Did you mean 'pba_priority'?", finding.Message);
        var line = File.ReadAllLines(TicketForm)[finding.Line!.Value - 1];
        Assert.Equal("datafieldname=\"pba_priorty\"", line.Substring(finding.Column!.Value - 1, "datafieldname=\"pba_priorty\"".Length));
    }

    [Fact]
    public void FormRow_Duplicated_ReportsDuplicateIdErrorAndRepeatedColumnWarning()
    {
        Fixture.Write(_workspace);
        var row = Fixture.Row("pba_customerid", "{270BD3DB-D9AF-4782-9025-509E298DEC0A}");
        Replace(TicketForm, row, row + row);

        var findings = Findings();

        var duplicateId = Assert.Single(findings, f => f.Code == ValidationDiagnostics.FormControlIdDuplicate);
        Assert.Equal(ValidationSeverity.Error, duplicateId.Severity);
        Assert.Contains("'pba_customerid'", duplicateId.Message);
        var repeated = Assert.Single(findings, f => f.Code == ValidationDiagnostics.FormColumnRepeated);
        Assert.Equal(ValidationSeverity.Warning, repeated.Severity);
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void FormColumn_RepeatedWithDistinctIds_OnlyWarns()
    {
        Fixture.Write(_workspace);
        var row = Fixture.Row("pba_customerid", "{270BD3DB-D9AF-4782-9025-509E298DEC0A}");
        Replace(TicketForm, row, row + row.Replace("control id=\"pba_customerid\"", "control id=\"pba_customerid1\""));

        var finding = Assert.Single(Findings());
        Assert.Equal(ValidationDiagnostics.FormColumnRepeated, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void FormColumn_InHeaderAndBody_OrLookupWithQuickView_NotReported()
    {
        // The fixture's ticket form has pba_priority in the header and the body, and pba_customerid as a lookup and a quick view.
        Fixture.Write(_workspace);
        Assert.Contains("header_pba_priority", File.ReadAllText(TicketForm));

        Assert.DoesNotContain(Findings(), f => f.Code == ValidationDiagnostics.FormColumnRepeated);
    }

    [Fact]
    public void BusinessProcessFlowStageControls_RepeatedAcrossTabs_NotReported()
    {
        // An exported business process flow form repeats each stage control, with its id, in every tab showing the stage.
        // The controls bind columns of the parent table through the relationship attribute, not of the form's table.
        Fixture.Write(_workspace);
        string StageRow(string column, string classId) =>
            $"<row>\n<cell id=\"{{{Guid.NewGuid()}}}\">\n<control id=\"bpf_pba_customer_pba_ticket:{column}\" classid=\"{classId}\" " +
            $"datafieldname=\"{column}\" disabled=\"false\" relationship=\"bpf_pba_customer_pba_ticket\" isrequired=\"false\" />\n</cell>\n</row>\n";
        var stage = StageRow("pba_segmentcode", "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}") + StageRow("statuscode", "{5D68B988-0661-4DB2-BC3E-17598AD3BE6C}");
        Replace(TicketForm, "</rows>\n</section>", stage + stage + stage + "</rows>\n</section>");

        Assert.Empty(Findings());
    }

    [Fact]
    public void FormComponentControl_SharingLookupControlId_NotReported()
    {
        // The form designer exports a form component control with the id of the lookup control that selects its record.
        Fixture.Write(_workspace);
        Replace(TicketForm, "</rows>\n</section>",
            "<row>\n<cell id=\"cell_customer_form\">\n<control id=\"pba_customerid\" classid=\"{F9A8A302-114E-466A-B582-6771B2AE0D92}\" " +
            "datafieldname=\"pba_customerid\" uniqueid=\"{0b0b0b0b-0000-0000-0000-000000000001}\" />\n</cell>\n</row>\n</rows>\n</section>");

        Assert.Empty(Findings());
    }

    // ---- Subgrids ----

    [Fact]
    public void Subgrid_MisspelledTargetEntity_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(CustomerForm, "<TargetEntityType>pba_ticket<", "<TargetEntityType>pba_tickte<");

        var finding = Single(Findings(), ValidationDiagnostics.SubgridTargetEntityUnknown);
        Assert.Contains("Did you mean 'pba_ticket'?", finding.Message);
    }

    [Fact]
    public void Subgrid_UnknownRelationship_ReportsErrorWithSuggestion()
    {
        Fixture.Write(_workspace);
        Replace(CustomerForm, "<RelationshipName>pba_customer_pba_ticket<", "<RelationshipName>pba_customer_pba_ticket_wrong<");

        var finding = Single(Findings(), ValidationDiagnostics.SubgridRelationshipInvalid);
        Assert.Contains("not defined in the workspace", finding.Message);
        Assert.Contains("Did you mean 'pba_customer_pba_ticket'?", finding.Message);
    }

    [Fact]
    public void Subgrid_RelationshipInWrongDirection_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(Path.Combine(_workspace, "Other", "Relationships", "pba_customer.xml"),
            "<ReferencingEntityName>pba_ticket</ReferencingEntityName>\n    <ReferencedEntityName>pba_customer</ReferencedEntityName>",
            "<ReferencingEntityName>pba_customer</ReferencingEntityName>\n    <ReferencedEntityName>pba_ticket</ReferencedEntityName>");

        var finding = Assert.Single(Findings(), f => f.Code == ValidationDiagnostics.SubgridRelationshipInvalid);
        Assert.Contains("one-to-many from 'pba_ticket' to 'pba_customer'", finding.Message);
    }

    [Fact]
    public void Subgrid_ViewOfAnotherEntity_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(CustomerForm, $"<ViewId>{TicketViewId}<", $"<ViewId>{CustomerViewId}<");

        var finding = Single(Findings(), ValidationDiagnostics.SubgridViewInvalid);
        Assert.Contains("view of 'pba_customer'", finding.Message);
    }

    [Fact]
    public void Subgrid_UnknownView_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(CustomerForm, $"<ViewId>{TicketViewId}<", "<ViewId>{99999999-9999-9999-9999-999999999999}<");

        var finding = Single(Findings(), ValidationDiagnostics.SubgridViewInvalid);
        Assert.Contains("not a saved query of 'pba_ticket'", finding.Message);
    }

    // ---- Views ----

    [Theory]
    [InlineData("<attribute name=\"pba_priority\" />", "<attribute name=\"pba_priorit\" />")]
    [InlineData("<order attribute=\"pba_name\"", "<order attribute=\"pba_nam\"")]
    [InlineData("<condition attribute=\"pba_priority\"", "<condition attribute=\"pba_priorityy\"")]
    [InlineData("<condition entityname=\"cust\" attribute=\"pba_name\"", "<condition entityname=\"cust\" attribute=\"pba_nme\"")]
    [InlineData("<attribute name=\"pba_name\" alias=\"cust_name\" />", "<attribute name=\"pba_nmae\" alias=\"cust_name\" />")]
    public void ViewFetch_UnknownColumn_ReportsError(string oldValue, string newValue)
    {
        Fixture.Write(_workspace);
        Replace(TicketView, oldValue, newValue);

        var finding = Single(Findings(), ValidationDiagnostics.ViewFetchColumnUnknown);
        Assert.Equal(TicketView, finding.FilePath);
        Assert.Contains("Did you mean", finding.Message);
    }

    [Theory]
    [InlineData("<cell name=\"pba_priority\"", "<cell name=\"pba_prioritee\"", "pba_ticket")]
    [InlineData("<cell name=\"cust.pba_name\"", "<cell name=\"cust.pba_nam\"", "pba_customer")]
    public void ViewLayout_UnknownColumn_ReportsErrorAgainstAliasedEntity(string oldValue, string newValue, string entity)
    {
        Fixture.Write(_workspace);
        Replace(TicketView, oldValue, newValue);

        var finding = Single(Findings(), ValidationDiagnostics.ViewLayoutColumnUnknown);
        Assert.Contains($"not a column of '{entity}'", finding.Message);
    }

    [Fact]
    public void ViewLayout_AliasNotInFetch_Skipped()
    {
        Fixture.Write(_workspace);
        Replace(TicketView, "<cell name=\"cust.pba_name\"", "<cell name=\"other.anything\"");

        Assert.Empty(Findings());
    }

    // ---- Site maps ----

    [Fact]
    public void SiteMap_MisspelledEntity_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(SiteMap, "Entity=\"pba_customer\"", "Entity=\"pba_custmer\"");

        var finding = Single(Findings(), ValidationDiagnostics.SiteMapEntityUnknown);
        Assert.Contains("Did you mean 'pba_customer'?", finding.Message);
    }

    [Fact]
    public void SiteMap_UrlViewOfAnotherEntity_ReportsError()
    {
        Fixture.Write(_workspace);
        Replace(SiteMap, "viewid=%7b33333333-3333-3333-3333-333333333333%7d", "viewid=%7b44444444-4444-4444-4444-444444444444%7d");

        var finding = Single(Findings(), ValidationDiagnostics.SiteMapViewInvalid);
        Assert.Contains("belongs to 'pba_customer'", finding.Message);
    }

    // ---- App modules ----

    [Theory]
    [InlineData("type=\"62\" schemaName=\"pba_app\"", "type=\"62\" schemaName=\"pba_ap\"")]
    [InlineData("type=\"1\" schemaName=\"pba_ticket\"", "type=\"1\" schemaName=\"pba_tickt\"")]
    [InlineData("type=\"26\" id=\"" + TicketViewId + "\"", "type=\"26\" id=\"{99999999-9999-9999-9999-999999999999}\"")]
    [InlineData("type=\"60\" id=\"" + TicketFormId + "\"", "type=\"60\" id=\"{99999999-9999-9999-9999-999999999999}\"")]
    public void AppModule_MissingComponent_ReportsWarning(string oldValue, string newValue)
    {
        Fixture.Write(_workspace);
        Replace(AppModule, oldValue, newValue);

        var finding = Single(Findings(), ValidationDiagnostics.AppModuleComponentMissing);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    // ---- System and external entities ----

    [Fact]
    public void ReferencesToSystemAndExternalEntities_NoFindings()
    {
        Fixture.Write(_workspace);

        // A form and view on account, which the workspace customizes without an Entity.xml.
        var accountDir = Path.Combine(_workspace, "Entities", "account");
        Directory.CreateDirectory(Path.Combine(accountDir, "FormXml", "main"));
        File.WriteAllText(Path.Combine(accountDir, "FormXml", "main", "{55555555-5555-5555-5555-555555555555}.xml"),
            Fixture.Form("{55555555-5555-5555-5555-555555555555}",
                Fixture.Row("name", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}") +
                Fixture.Row("ext_rating", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}") +
                Fixture.Subgrid("Contacts", "contact", "contact_customer_accounts", "{00000000-0000-0000-00aa-000010001004}") +
                Fixture.Subgrid("Orders", "ext_order", "ext_account_ext_order", "{66666666-6666-6666-6666-666666666666}")));
        Directory.CreateDirectory(Path.Combine(accountDir, "SavedQueries"));
        File.WriteAllText(Path.Combine(accountDir, "SavedQueries", "{77777777-7777-7777-7777-777777777777}.xml"),
            Fixture.View("{77777777-7777-7777-7777-777777777777}", "account",
                "<attribute name=\"name\" /><attribute name=\"ext_rating\" /><link-entity name=\"contact\" from=\"contactid\" to=\"primarycontactid\" alias=\"pc\"><attribute name=\"fullname\" /></link-entity>",
                "<cell name=\"name\" /><cell name=\"pc.fullname\" /><cell name=\"ext_rating\" />"));

        // Site map and app entries for system tables and an entity from a package outside the workspace.
        Replace(SiteMap, "</Group>",
            "<SubArea Id=\"accounts\" Entity=\"account\" /><SubArea Id=\"orders\" Entity=\"ext_order\" />" +
            "<SubArea Id=\"contacts\" Url=\"/main.aspx?pagetype=entitylist&amp;etn=contact&amp;viewid=%7b00000000-0000-0000-00aa-000010001004%7d\" /></Group>");
        Replace(AppModule, "</AppModuleComponents>",
            "<AppModuleComponent type=\"1\" schemaName=\"account\" /><AppModuleComponent type=\"1\" schemaName=\"ext_order\" />" +
            "<AppModuleComponent type=\"26\" id=\"{00000000-0000-0000-00aa-000010001004}\" /></AppModuleComponents>");

        Assert.Empty(Findings());
    }

    [Fact]
    public void ShellEntity_ColumnsNotChecked()
    {
        Fixture.Write(_workspace);
        Replace(Path.Combine(_workspace, "Other", "Solution.xml"),
            "schemaName=\"pba_ticket\" behavior=\"0\"", "schemaName=\"pba_ticket\" behavior=\"2\"");
        Replace(TicketForm, "datafieldname=\"pba_priority\" disabled", "datafieldname=\"pba_defined_in_base\" disabled");

        Assert.Empty(Findings());
    }

    [Fact]
    public void PackageTableIncludedWithoutBehavior_ColumnsNotChecked()
    {
        // A table from another publisher's package, listed with no behavior attribute (include everything), whose Entity.xml
        // holds only the columns this solution adds, plus the package's primary name column, whose label it customized.
        Fixture.Write(_workspace);
        Replace(Path.Combine(_workspace, "Other", "Solution.xml"), "</RootComponents>",
            "<RootComponent type=\"1\" schemaName=\"ext_order\" /></RootComponents>");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_workspace, "Entities", "ext_order")).FullName, "Entity.xml"),
            Fixture.Entity("ext_order", ("pba_rating", "int"), ("ext_name", "nvarchar")).Replace(
                "<LogicalName>ext_name</LogicalName>", "<LogicalName>ext_name</LogicalName>\n          <DisplayMask>PrimaryName</DisplayMask>"));
        var orderForm = Path.Combine(_workspace, "Entities", "ext_order", "FormXml", "main", "{55555555-5555-5555-5555-555555555555}.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(orderForm)!);
        File.WriteAllText(orderForm, Fixture.Form("{55555555-5555-5555-5555-555555555555}",
            Fixture.Row("ext_ordernumber", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}") +
            Fixture.Row("pba_rating", "{C3EFE0C3-0EC6-42BE-8349-CBD9079DFD8E}")));
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_workspace, "Entities", "ext_order", "SavedQueries")).FullName,
                "{66666666-6666-6666-6666-666666666666}.xml"),
            Fixture.View("{66666666-6666-6666-6666-666666666666}", "ext_order",
                "<attribute name=\"ext_ordernumber\" /><attribute name=\"pba_rating\" />", "<cell name=\"ext_ordernumber\" />"));

        Assert.Empty(Findings());
    }

    [Fact]
    public void SamePublisherTableIncludedTrimmed_ColumnsNotCheckedInThatSolution()
    {
        // A solution of the owning publisher includes a table that a sibling solution defines, without a shell behavior and
        // with a trimmed Entity.xml (no primary key column). Validated on its own, it cannot tell which columns exist.
        Fixture.WriteManifest(_workspace, "pba_ui", "<RootComponent type=\"1\" schemaName=\"pba_ticket\" behavior=\"0\" />");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_workspace, "Entities", "pba_ticket")).FullName, "Entity.xml"),
            Fixture.Entity("pba_ticket", ("pba_uionly", "nvarchar")));
        Directory.CreateDirectory(Path.GetDirectoryName(TicketForm)!);
        File.WriteAllText(TicketForm, Fixture.Form(TicketFormId,
            Fixture.Row("pba_priority", "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}") + Fixture.Row("pba_uionly", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}")));

        Assert.Empty(Findings());
    }

    [Fact]
    public void SystemColumnsAbsentFromTrimmedEntity_NotReported()
    {
        // The fixture's Entity.xml lists no statecode, ownerid or createdon; the form, view and layout use them.
        Fixture.Write(_workspace);
        Assert.DoesNotContain("<LogicalName>ownerid<", File.ReadAllText(Path.Combine(_workspace, "Entities", "pba_ticket", "Entity.xml")));

        Assert.Empty(Findings());
    }

    // ---- Workspace scope ----

    [Fact]
    public void FormInOneSolution_ReferencesEntityInAnother_ValidatedAcrossSolutions()
    {
        var core = Path.Combine(_workspace, "Core");
        var ui = Path.Combine(_workspace, "Ui");
        Fixture.Write(core);
        Fixture.WriteManifest(ui, "pba_ui", rootEntities: "");
        var formDir = Path.Combine(ui, "Entities", "pba_ticket", "FormXml", "main");
        Directory.CreateDirectory(formDir);
        var uiForm = Path.Combine(formDir, "{88888888-8888-8888-8888-888888888888}.xml");
        File.WriteAllText(uiForm, Fixture.Form("{88888888-8888-8888-8888-888888888888}",
            Fixture.Row("pba_priority", "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}")));

        Assert.Empty(Findings());

        Replace(uiForm, "datafieldname=\"pba_priority\"", "datafieldname=\"pba_priorty\"");
        var finding = Single(Findings(), ValidationDiagnostics.FormControlColumnUnknown);
        Assert.Equal(uiForm, finding.FilePath);
    }

    [Fact]
    public void InMemoryWorkspace_UsesModelWithoutLineInformation()
    {
        var ws = new Workspace("memory");
        ws.AddSolution(new Solution { UniqueName = "pba_core", Publisher = new Publisher { UniqueName = "pba", Prefix = "pba" } });
        var entity = new EntityMetadata { LogicalName = "pba_ticket", IsCustomEntity = true, PrimaryIdAttribute = "pba_ticketid" };
        entity.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = "pba_ticketid" });
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "pba_name" });
        ws.AddEntity(entity);
        ws.AddForm(new FormMetadata
        {
            FormId = TicketFormId,
            EntityLogicalName = "pba_ticket",
            Body = MergeableNodeXmlConverter.FromXElement(XElement.Parse(
                "<form><tabs><tab><columns><column><sections><section><rows>" +
                "<row><cell><control id=\"pba_nam\" datafieldname=\"pba_nam\" /></cell></row>" +
                "</rows></section></sections></column></columns></tab></tabs></form>"))
        });
        ws.AddView(new SavedQueryMetadata
        {
            SavedQueryId = TicketViewId,
            EntityLogicalName = "pba_ticket",
            FetchXml = "<fetch><entity name=\"pba_ticket\"><attribute name=\"pba_name\" /><attribute name=\"pba_titel\" /></entity></fetch>",
            LayoutXml = "<grid><row><cell name=\"pba_name\" /></row></grid>"
        });

        var findings = new ComponentReferenceValidator().Validate(ws);

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Code == ValidationDiagnostics.FormControlColumnUnknown && f.Message.Contains("Did you mean 'pba_name'?"));
        Assert.Contains(findings, f => f.Code == ValidationDiagnostics.ViewFetchColumnUnknown && f.Message.Contains("'pba_titel'"));
        Assert.All(findings, f => Assert.Null(f.Line));
    }

    /// <summary>A minimal valid two-entity solution: customers with tickets, a subgrid, views, a site map and an app.</summary>
    private static class Fixture
    {
        public static void Write(string root)
        {
            WriteManifest(root, "pba_core",
                "<RootComponent type=\"1\" schemaName=\"pba_customer\" behavior=\"0\" />" +
                "<RootComponent type=\"1\" schemaName=\"pba_ticket\" behavior=\"0\" />" +
                "<RootComponent type=\"62\" schemaName=\"pba_app\" behavior=\"0\" />" +
                "<RootComponent type=\"80\" schemaName=\"pba_app\" behavior=\"0\" />");

            WriteFile(root, "Entities/pba_customer/Entity.xml", Entity("pba_customer",
                ("pba_customerid", "primarykey"), ("pba_name", "nvarchar")));
            WriteFile(root, "Entities/pba_ticket/Entity.xml", Entity("pba_ticket",
                ("pba_ticketid", "primarykey"), ("pba_name", "nvarchar"), ("pba_priority", "picklist"), ("pba_customerid", "lookup")));

            WriteFile(root, $"Entities/pba_ticket/FormXml/main/{TicketFormId}.xml", Form(TicketFormId,
                Row("pba_name", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}") +
                Row("pba_priority", "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}") +
                Row("pba_customerid", "{270BD3DB-D9AF-4782-9025-509E298DEC0A}") +
                Row("pba_customerid", "{5C5600E0-1D6E-4205-A272-BE80DA87FD42}", id: "customer_card") +
                Row("ownerid", "{270BD3DB-D9AF-4782-9025-509E298DEC0A}") +
                Row("statuscode", "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}"),
                header: "<control id=\"header_pba_priority\" classid=\"{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}\" datafieldname=\"pba_priority\" />"));
            WriteFile(root, $"Entities/pba_customer/FormXml/main/{CustomerFormId}.xml", Form(CustomerFormId,
                Row("pba_name", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}") +
                Subgrid("Tickets", "pba_ticket", "pba_customer_pba_ticket", TicketViewId)));

            WriteFile(root, $"Entities/pba_ticket/SavedQueries/{TicketViewId}.xml", View(TicketViewId, "pba_ticket",
                "<attribute name=\"pba_name\" />\n" +
                "<attribute name=\"pba_priority\" />\n" +
                "<attribute name=\"createdon\" />\n" +
                "<order attribute=\"pba_name\" descending=\"false\" />\n" +
                "<filter type=\"and\">\n" +
                "<condition attribute=\"statecode\" operator=\"eq\" value=\"0\" />\n" +
                "<filter type=\"or\"><condition attribute=\"pba_priority\" operator=\"eq\" value=\"1\" /></filter>\n" +
                "<condition entityname=\"cust\" attribute=\"pba_name\" operator=\"not-null\" />\n" +
                "</filter>\n" +
                "<link-entity name=\"pba_customer\" from=\"pba_customerid\" to=\"pba_customerid\" alias=\"cust\">\n" +
                "<attribute name=\"pba_name\" alias=\"cust_name\" />\n" +
                "</link-entity>",
                "<cell name=\"pba_name\" width=\"200\" />\n" +
                "<cell name=\"cust.pba_name\" width=\"200\" />\n" +
                "<cell name=\"pba_priority\" width=\"100\" />\n" +
                "<cell name=\"ownerid\" width=\"100\" />"));
            WriteFile(root, $"Entities/pba_customer/SavedQueries/{CustomerViewId}.xml", View(CustomerViewId, "pba_customer",
                "<attribute name=\"pba_name\" />", "<cell name=\"pba_name\" width=\"200\" />"));

            WriteFile(root, "Other/Relationships.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<EntityRelationships>\n  <EntityRelationship Name=\"pba_customer_pba_ticket\" />\n</EntityRelationships>");
            WriteFile(root, "Other/Relationships/pba_customer.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<EntityRelationships>\n  <EntityRelationship Name=\"pba_customer_pba_ticket\">\n" +
                "    <EntityRelationshipType>OneToMany</EntityRelationshipType>\n" +
                "    <ReferencingEntityName>pba_ticket</ReferencingEntityName>\n" +
                "    <ReferencedEntityName>pba_customer</ReferencedEntityName>\n" +
                "    <ReferencingAttributeName>pba_customerid</ReferencingAttributeName>\n" +
                "  </EntityRelationship>\n</EntityRelationships>");

            WriteFile(root, "AppModuleSiteMaps/pba_app/AppModuleSiteMap.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AppModuleSiteMap>\n  <SiteMapUniqueName>pba_app</SiteMapUniqueName>\n" +
                "  <SiteMap>\n    <Area Id=\"area\">\n      <Group Id=\"group\">\n" +
                "        <SubArea Id=\"tickets\" Url=\"/main.aspx?pagetype=entitylist&amp;etn=pba_ticket&amp;viewid=%7b33333333-3333-3333-3333-333333333333%7d\" />\n" +
                "        <SubArea Id=\"customers\" Entity=\"pba_customer\" />\n" +
                "      </Group>\n    </Area>\n  </SiteMap>\n</AppModuleSiteMap>");
            WriteFile(root, "AppModules/pba_app/AppModule.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AppModule>\n  <UniqueName>pba_app</UniqueName>\n  <AppModuleComponents>\n" +
                "    <AppModuleComponent type=\"62\" schemaName=\"pba_app\" />\n" +
                "    <AppModuleComponent type=\"1\" schemaName=\"pba_ticket\" />\n" +
                "    <AppModuleComponent type=\"1\" schemaName=\"pba_customer\" />\n" +
                $"    <AppModuleComponent type=\"26\" id=\"{TicketViewId}\" />\n" +
                $"    <AppModuleComponent type=\"60\" id=\"{TicketFormId}\" />\n" +
                "  </AppModuleComponents>\n</AppModule>");
        }

        public static void WriteManifest(string root, string uniqueName, string rootEntities) =>
            WriteFile(root, "Other/Solution.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ImportExportXml>\n  <SolutionManifest>\n" +
                $"    <UniqueName>{uniqueName}</UniqueName>\n    <Version>1.0.0.0</Version>\n    <Managed>0</Managed>\n" +
                "    <Publisher>\n      <UniqueName>pba</UniqueName>\n      <CustomizationPrefix>pba</CustomizationPrefix>\n    </Publisher>\n" +
                $"    <RootComponents>{rootEntities}</RootComponents>\n    <MissingDependencies />\n  </SolutionManifest>\n</ImportExportXml>");

        public static string Entity(string name, params (string Name, string Type)[] attributes) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Entity>\n" +
            $"  <Name LocalizedName=\"{name}\">{name}</Name>\n  <EntityInfo>\n    <entity Name=\"{name}\">\n      <attributes>\n" +
            string.Concat(attributes.Select(a =>
                $"        <attribute PhysicalName=\"{a.Name}\">\n          <Type>{a.Type}</Type>\n          <LogicalName>{a.Name}</LogicalName>\n" +
                (a.Type == "nvarchar" && a.Name == "pba_name" ? "          <DisplayMask>PrimaryName</DisplayMask>\n" : "") +
                "          <IsCustomField>1</IsCustomField>\n        </attribute>\n")) +
            "      </attributes>\n    </entity>\n  </EntityInfo>\n</Entity>";

        public static string Row(string column, string classId, string? id = null) =>
            $"<row>\n<cell id=\"cell_{id ?? column}\">\n<control id=\"{id ?? column}\" classid=\"{classId}\" datafieldname=\"{column}\" disabled=\"false\" />\n</cell>\n</row>\n";

        public static string Subgrid(string id, string target, string relationship, string viewId) =>
            $"<row>\n<cell id=\"{{{Guid.NewGuid()}}}\">\n<control id=\"{id}\" classid=\"{{E7A81278-8635-4D9E-8D4D-59480B391C5B}}\" indicationOfSubgrid=\"true\">\n<parameters>\n" +
            $"<ViewId>{viewId}</ViewId>\n<IsUserView>false</IsUserView>\n<RelationshipName>{relationship}</RelationshipName>\n<TargetEntityType>{target}</TargetEntityType>\n" +
            "</parameters>\n</control>\n</cell>\n</row>\n";

        public static string Form(string formId, string rows, string header = "") =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<forms>\n<systemform>\n" +
            $"<formid>{formId}</formid>\n<form>\n" +
            "<tabs>\n<tab name=\"general\">\n<columns>\n<column width=\"100%\">\n<sections>\n<section name=\"general\">\n<rows>\n" +
            rows +
            "</rows>\n</section>\n</sections>\n</column>\n</columns>\n</tab>\n</tabs>\n" +
            (header.Length == 0 ? "" : $"<header id=\"{{{Guid.NewGuid()}}}\">\n<rows>\n<row>\n<cell id=\"{{{Guid.NewGuid()}}}\">\n{header}\n</cell>\n</row>\n</rows>\n</header>\n") +
            "</form>\n</systemform>\n</forms>";

        public static string View(string viewId, string entity, string fetchBody, string cells) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<savedqueries>\n<savedquery>\n" +
            $"<savedqueryid>{viewId}</savedqueryid>\n<querytype>0</querytype>\n" +
            $"<layoutxml>\n<grid name=\"resultset\" object=\"1\" jump=\"name\" select=\"1\" icon=\"1\" preview=\"1\">\n<row name=\"result\" id=\"{entity}id\">\n{cells}\n</row>\n</grid>\n</layoutxml>\n" +
            $"<fetchxml>\n<fetch version=\"1.0\" mapping=\"logical\">\n<entity name=\"{entity}\">\n{fetchBody}\n</entity>\n</fetch>\n</fetchxml>\n" +
            "</savedquery>\n</savedqueries>";

        private static void WriteFile(string root, string relativePath, string content)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
