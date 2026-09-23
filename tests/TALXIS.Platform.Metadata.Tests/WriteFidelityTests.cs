using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

public class WriteFidelityTests
{
    private static readonly string Root = WorkspaceFixtures.VirtualRoot("fidelity");

    private const string Bom = "﻿";

    private const string FormXml =
        "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<forms xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "  <systemform>\r\n" +
        "    <formid>{11111111-0000-0000-0000-000000000001}</formid>\r\n" +
        "    <form>\r\n" +
        "      <tabs>\r\n" +
        "        <tab id=\"general\" name=\"General\">\r\n" +
        "          <columns />\r\n" +
        "        </tab>\r\n" +
        "      </tabs>\r\n" +
        "    </form>\r\n" +
        "    <LocalizedNames>\r\n" +
        "      <LocalizedName description=\"Main\" languagecode=\"1033\" />\r\n" +
        "    </LocalizedNames>\r\n" +
        "  </systemform>\r\n" +
        "</forms>";

    private const string RibbonXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<RibbonDiffXml xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "\t<CustomActions>\r\n" +
        "\t\t<HideCustomAction HideActionId=\"ntg.Hide.A\" Location=\"Mscrm.Form.a\" />\r\n" +
        "\t</CustomActions>\r\n" +
        "</RibbonDiffXml>";

    private const string RoleXml =
        "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<Role id=\"{22222222-0000-0000-0000-000000000002}\" name=\"Yungo Engineering+\" isinherited=\"1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "  <IsCustomizable>1</IsCustomizable>\r\n" +
        "  <RolePrivileges>\r\n" +
        "    <!-- ADDRESSES -->\r\n" +
        "    <RolePrivilege name=\"prvReadAddress\" level=\"Global\" />\r\n" +
        "    <RolePrivilege name=\"prvReadAddress\" level=\"Global\" />\r\n" +
        "\r\n" +
        "    <!-- ORDERS -->\r\n" +
        "    <RolePrivilege name=\"prvReadOrder\" level=\"Basic\" />\r\n" +
        "  </RolePrivileges>\r\n" +
        "</Role>";

    private const string OptionSetXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<optionset Name=\"ntg_billingfrequencytypecode\" localizedName=\"Stale Mirror\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "  <OptionSetType>picklist</OptionSetType>\r\n" +
        "  <IsGlobal>true</IsGlobal>\r\n" +
        "  <displaynames>\r\n" +
        "    <displayname description=\"Billing Frequency\" languagecode=\"1033\" />\r\n" +
        "  </displaynames>\r\n" +
        "  <options>\r\n" +
        "    <option value=\"1\">\r\n" +
        "      <labels>\r\n" +
        "        <label description=\"Monthly\" languagecode=\"1033\" />\r\n" +
        "      </labels>\r\n" +
        "    </option>\r\n" +
        "  </options>\r\n" +
        "</optionset>";

    private const string RelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<EntityRelationships xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "  <!-- Sample -->\r\n" +
        "  <EntityRelationship Name=\"\"/>\r\n" +
        "</EntityRelationships>";

    private const string OrgOwnedEntityXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<Entity xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
        "  <Name LocalizedName=\"Old Mirror\" OriginalName=\"Original\">test_entity</Name>\r\n" +
        "  <EntityInfo>\r\n" +
        "    <entity Name=\"test_entity\">\r\n" +
        "      <LocalizedNames>\r\n" +
        "        <LocalizedName description=\"Test Entity\" languagecode=\"1033\" />\r\n" +
        "      </LocalizedNames>\r\n" +
        "      <OwnershipTypeMask>OrgOwned</OwnershipTypeMask>\r\n" +
        "      <attributes />\r\n" +
        "    </entity>\r\n" +
        "  </EntityInfo>\r\n" +
        "</Entity>";

    [Fact]
    public void UnchangedForcedWrite_ReproducesBytesIncludingBom()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var formFile = FormPath();
        var before = context.GetFileText(formFile);

        workspace.Forms[0].MarkDirty();
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(before, context.GetFileText(formFile));
        Assert.StartsWith(Bom, context.GetFileText(formFile)!);
    }

    [Fact]
    public void ChangedFormBody_KeepsIndentationAndBom_AndOnlyAddsTheNewNode()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var before = context.GetFileText(FormPath())!;
        var tab = new MergeableNode { Name = "tab" };
        tab.SetAttribute("id", "extra");
        var columns = new MergeableNode { Name = "columns" };
        tab.AddChild(columns);
        workspace.Forms[0].Body!.FindNode(n => n.Name == "tabs")!.AddChild(tab);

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        var after = context.GetFileText(FormPath())!;
        Assert.StartsWith(Bom, after);
        Assert.Contains("\r\n        <tab id=\"extra\">\r\n          <columns />\r\n        </tab>\r\n", after);
        var removed = before.Split("\r\n").Except(after.Split("\r\n")).ToArray();
        Assert.Empty(removed);
        Assert.Equal(3, after.Split("\r\n").Length - before.Split("\r\n").Length);
    }

    [Fact]
    public void ChangedRibbonBody_KeepsNamespaceDeclarationsAndTabIndentation()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var hide = new MergeableNode { Name = "HideCustomAction" };
        hide.SetAttribute("HideActionId", "ntg.Hide.B");
        hide.SetAttribute("Location", "Mscrm.Form.b");
        workspace.Ribbons[0].Body!.FindNode(n => n.Name == "CustomActions")!.AddChild(hide);

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        var after = context.GetFileText(RibbonPath())!;
        Assert.Contains("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", after);
        Assert.Contains("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", after);
        Assert.DoesNotContain(" xsd=\"", after);
        Assert.Contains("\r\n\t\t<HideCustomAction HideActionId=\"ntg.Hide.B\" Location=\"Mscrm.Form.b\" />\r\n", after);
    }

    [Fact]
    public void RootComponents_KeepCommentsWhenOneIsAdded_AndDropOnlyTheRemovedLine()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var solution = workspace.Solutions[0];
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Role, Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), Behavior = 0 });
        solution.RemoveRootComponent(ComponentType.Entity, "test_entity");

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        var after = context.GetFileText(Path.Combine(Root, "Other", "Solution.xml"))!;
        Assert.Contains("<!-- { ENTITIES } -->", after);
        Assert.Contains("<!-- { ROLES } -->", after);
        Assert.DoesNotContain("schemaName=\"test_entity\"", after);
        Assert.Contains("\r\n      <RootComponent type=\"20\" id=\"{22222222-0000-0000-0000-000000000002}\" behavior=\"0\" />\r\n", after);
    }

    [Fact]
    public void RolePrivileges_KeepCommentsBlankLinesAndDuplicates_AndPatchLevelInPlace()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var role = workspace.SecurityRoles[0];
        role.Privileges.First(p => p.Name == "prvReadOrder").Level = "Global";

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        var after = context.GetFileText(RolePath())!;
        Assert.Contains("<!-- ADDRESSES -->", after);
        Assert.Contains("<!-- ORDERS -->", after);
        Assert.Contains("\r\n\r\n    <!-- ORDERS -->", after);
        Assert.Equal(2, after.Split("prvReadAddress").Length - 1);
        Assert.Contains("<RolePrivilege name=\"prvReadOrder\" level=\"Global\" />", after);
        Assert.StartsWith(Bom, after);
    }

    [Fact]
    public void ComponentsWithNonCanonicalFileNames_WriteBackToTheirOwnFiles()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        workspace.SecurityRoles[0].MarkDirty();
        workspace.GlobalOptionSets[0].MarkDirty();
        workspace.Views[0].MarkDirty();
        var filesBefore = context.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(new[] { OptionSetPath(), RolePath(), ViewPath() }.OrderBy(f => f, StringComparer.OrdinalIgnoreCase), modified.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(filesBefore, context.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MirrorAttributesAndForeignTokens_StayAsLoaded()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        Assert.True(workspace.GlobalOptionSets[0].IsGlobal);
        Assert.Equal(Components.OwnershipType.OrganizationOwned, workspace.Entities[0].Ownership);
        workspace.Entities[0].DisplayName[1033] = "Renamed Entity";
        workspace.GlobalOptionSets[0].DisplayName[1033] = "Renamed Option Set";

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        var entity = context.GetFileText(Path.Combine(Root, "Entities", "test_entity", "Entity.xml"))!;
        var optionSet = context.GetFileText(OptionSetPath())!;
        Assert.Contains("<Name LocalizedName=\"Old Mirror\" OriginalName=\"Original\">test_entity</Name>", entity);
        Assert.Contains("<LocalizedName description=\"Renamed Entity\" languagecode=\"1033\" />", entity);
        Assert.Contains("<OwnershipTypeMask>OrgOwned</OwnershipTypeMask>", entity);
        Assert.Contains("localizedName=\"Stale Mirror\"", optionSet);
        Assert.Contains("<IsGlobal>true</IsGlobal>", optionSet);
        Assert.Contains("<displayname description=\"Renamed Option Set\" languagecode=\"1033\" />", optionSet);
    }

    [Fact]
    public void RelationshipsFileWithoutNamedEntries_IsNotDeleted()
    {
        var context = Seed();
        var workspace = new XmlWorkspaceReader(context).Load(Root);

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Empty(modified);
        Assert.Equal(RelationshipsXml, context.GetFileText(Path.Combine(Root, "Other", "Relationships.xml")));
    }

    private static InMemoryContext Seed()
    {
        var context = new InMemoryContext();
        context.SetFile(Path.Combine(Root, "Other", "Solution.xml"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<ImportExportXml version=\"9.1\">\r\n" +
            "  <SolutionManifest>\r\n" +
            "    <UniqueName>Fidelity</UniqueName>\r\n" +
            "    <Version>1.0</Version>\r\n" +
            "    <Managed>0</Managed>\r\n" +
            "    <Publisher>\r\n" +
            "      <UniqueName>test</UniqueName>\r\n" +
            "      <CustomizationPrefix>test</CustomizationPrefix>\r\n" +
            "    </Publisher>\r\n" +
            "    <RootComponents>\r\n" +
            "      <!-- { ENTITIES } -->\r\n" +
            "      <RootComponent type=\"1\" schemaName=\"test_entity\" behavior=\"0\" />\r\n" +
            "      <!-- { ROLES } -->\r\n" +
            "    </RootComponents>\r\n" +
            "  </SolutionManifest>\r\n" +
            "</ImportExportXml>");
        context.SetFile(Path.Combine(Root, "Entities", "test_entity", "Entity.xml"), OrgOwnedEntityXml);
        context.SetFile(FormPath(), FormXml);
        context.SetFile(RibbonPath(), RibbonXml);
        context.SetFile(RolePath(), RoleXml);
        context.SetFile(OptionSetPath(), OptionSetXml);
        context.SetFile(Path.Combine(Root, "Other", "Relationships.xml"), RelationshipsXml);
        context.SetFile(ViewPath(),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<savedqueries xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
            "  <savedquery>\r\n" +
            "    <savedqueryid>{33333333-0000-0000-0000-000000000003}</savedqueryid>\r\n" +
            "    <LocalizedNames>\r\n" +
            "      <LocalizedName description=\"Active\" languagecode=\"1033\" />\r\n" +
            "    </LocalizedNames>\r\n" +
            "  </savedquery>\r\n" +
            "</savedqueries>");
        return context;
    }

    private static string FormPath() => Path.Combine(Root, "Entities", "test_entity", "FormXml", "main", "{11111111-0000-0000-0000-000000000001}.xml");
    private static string RibbonPath() => Path.Combine(Root, "Entities", "test_entity", "RibbonDiff.xml");
    private static string RolePath() => Path.Combine(Root, "Roles", "Yungo EngineeringPlus.xml");
    private static string OptionSetPath() => Path.Combine(Root, "OptionSets", "ntg_billingfrequency.xml");
    private static string ViewPath() => Path.Combine(Root, "Entities", "test_entity", "SavedQueries", "{33333333-0000-0000-0000-000000000003}_managed.xml");
}
