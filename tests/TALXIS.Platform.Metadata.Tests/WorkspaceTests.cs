using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class WorkspaceTests
{
    public static TheoryData<string, Action<Workspace>, int> DuplicateAddCases =>
        new()
        {
            { "entity", workspace => AddDuplicateEntity(workspace), 1 },
            { "option set", workspace => AddDuplicateOptionSet(workspace), 1 },
            { "relationship", workspace => AddDuplicateRelationship(workspace), 1 },
            { "form", workspace => AddDuplicateForm(workspace), 1 },
            { "view", workspace => AddDuplicateView(workspace), 1 },
            { "plugin assembly", workspace => AddDuplicatePluginAssembly(workspace), 1 },
            { "SDK message processing step", workspace => AddDuplicateStep(workspace), 1 },
            { "security role", workspace => AddDuplicateSecurityRole(workspace), 1 },
            { "app module", workspace => AddDuplicateAppModule(workspace), 1 },
            { "site map", workspace => AddDuplicateSiteMap(workspace), 1 },
            { "web resource", workspace => AddDuplicateWebResource(workspace), 1 },
            { "workflow", workspace => AddDuplicateWorkflow(workspace), 1 },
            { "flow definition", workspace => AddDuplicateFlowDefinition(workspace), 1 },
            { "generic component", workspace => AddDuplicateGenericComponent(workspace), 1 }
        };

    [Theory]
    [MemberData(nameof(DuplicateAddCases))]
    public void AddMethods_DuplicateIdentity_Throws(string componentType, Action<Workspace> addDuplicate, int expectedCount)
    {
        var workspace = new Workspace("/tmp/workspace");

        var exception = Assert.Throws<InvalidOperationException>(() => addDuplicate(workspace));

        Assert.Contains(componentType, exception.Message);
        Assert.Equal(expectedCount, GetCount(workspace, componentType));
    }

    [Fact]
    public void AddMethods_DistinctIdentities_AreAccepted()
    {
        var workspace = new Workspace("/tmp/workspace");

        workspace.AddEntity(new EntityMetadata { LogicalName = "account" });
        workspace.AddEntity(new EntityMetadata { LogicalName = "contact" });
        workspace.AddFlowDefinition(new FlowDefinitionMetadata { FilePath = "Workflows/first.json" });
        workspace.AddFlowDefinition(new FlowDefinitionMetadata { FilePath = "Workflows/second.json" });
        workspace.AddGenericComponent(new GenericComponentMetadata
        {
            ComponentTypeName = "CustomComponent",
            FilePath = "Other/First.xml"
        });
        workspace.AddGenericComponent(new GenericComponentMetadata
        {
            ComponentTypeName = "CustomComponent",
            FilePath = "Other/Second.xml"
        });

        Assert.Equal(2, workspace.Entities.Count);
        Assert.Equal(2, workspace.FlowDefinitions.Count);
        Assert.Equal(2, workspace.GenericComponents.Count);
    }

    [Fact]
    public void AddFlowDefinition_MissingFilePath_Throws()
    {
        var workspace = new Workspace("/tmp/workspace");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            workspace.AddFlowDefinition(new FlowDefinitionMetadata()));

        Assert.Contains("flow definition", exception.Message);
        Assert.Contains("non-empty identity key", exception.Message);
        Assert.Empty(workspace.FlowDefinitions);
    }

    [Fact]
    public void AddGenericComponent_MissingFilePath_Throws()
    {
        var workspace = new Workspace("/tmp/workspace");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            workspace.AddGenericComponent(new GenericComponentMetadata
            {
                ComponentTypeName = "Connector"
            }));

        Assert.Contains("generic component", exception.Message);
        Assert.Contains("non-empty identity key", exception.Message);
        Assert.Empty(workspace.GenericComponents);
    }

    private static int GetCount(Workspace workspace, string componentType) => componentType switch
    {
        "entity" => workspace.Entities.Count,
        "option set" => workspace.GlobalOptionSets.Count,
        "relationship" => workspace.Relationships.Count,
        "form" => workspace.Forms.Count,
        "view" => workspace.Views.Count,
        "plugin assembly" => workspace.PluginAssemblies.Count,
        "SDK message processing step" => workspace.SdkMessageProcessingSteps.Count,
        "security role" => workspace.SecurityRoles.Count,
        "app module" => workspace.AppModules.Count,
        "site map" => workspace.SiteMaps.Count,
        "web resource" => workspace.WebResources.Count,
        "workflow" => workspace.Workflows.Count,
        "flow definition" => workspace.FlowDefinitions.Count,
        "generic component" => workspace.GenericComponents.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
    };

    private static void AddDuplicateEntity(Workspace workspace)
    {
        workspace.AddEntity(new EntityMetadata { LogicalName = "account" });
        workspace.AddEntity(new EntityMetadata { LogicalName = "ACCOUNT" });
    }

    private static void AddDuplicateOptionSet(Workspace workspace)
    {
        workspace.AddGlobalOptionSet(new OptionSetMetadata { Name = "status" });
        workspace.AddGlobalOptionSet(new OptionSetMetadata { Name = "STATUS" });
    }

    private static void AddDuplicateRelationship(Workspace workspace)
    {
        workspace.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_primary_contact",
            ReferencedEntity = "account",
            ReferencedAttribute = "primarycontactid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "contactid"
        });
        workspace.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "ACCOUNT_PRIMARY_CONTACT",
            ReferencedEntity = "account",
            ReferencedAttribute = "primarycontactid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "contactid"
        });
    }

    private static void AddDuplicateForm(Workspace workspace)
    {
        workspace.AddForm(new FormMetadata { FormId = "{11111111-1111-1111-1111-111111111111}" });
        workspace.AddForm(new FormMetadata { FormId = "{11111111-1111-1111-1111-111111111111}" });
    }

    private static void AddDuplicateView(Workspace workspace)
    {
        workspace.AddView(new SavedQueryMetadata { SavedQueryId = "{22222222-2222-2222-2222-222222222222}" });
        workspace.AddView(new SavedQueryMetadata { SavedQueryId = "{22222222-2222-2222-2222-222222222222}" });
    }

    private static void AddDuplicatePluginAssembly(Workspace workspace)
    {
        workspace.AddPluginAssembly(new PluginAssemblyMetadata { PluginAssemblyId = "{33333333-3333-3333-3333-333333333333}" });
        workspace.AddPluginAssembly(new PluginAssemblyMetadata { PluginAssemblyId = "{33333333-3333-3333-3333-333333333333}" });
    }

    private static void AddDuplicateStep(Workspace workspace)
    {
        workspace.AddSdkMessageProcessingStep(new SdkMessageProcessingStepMetadata { SdkMessageProcessingStepId = "{44444444-4444-4444-4444-444444444444}" });
        workspace.AddSdkMessageProcessingStep(new SdkMessageProcessingStepMetadata { SdkMessageProcessingStepId = "{44444444-4444-4444-4444-444444444444}" });
    }

    private static void AddDuplicateSecurityRole(Workspace workspace)
    {
        workspace.AddSecurityRole(new SecurityRoleMetadata { RoleId = "{55555555-5555-5555-5555-555555555555}", Name = "Salesperson" });
        workspace.AddSecurityRole(new SecurityRoleMetadata { RoleId = "{55555555-5555-5555-5555-555555555555}", Name = "Salesperson" });
    }

    private static void AddDuplicateAppModule(Workspace workspace)
    {
        workspace.AddAppModule(new AppModuleMetadata { UniqueName = "sales_app" });
        workspace.AddAppModule(new AppModuleMetadata { UniqueName = "SALES_APP" });
    }

    private static void AddDuplicateSiteMap(Workspace workspace)
    {
        workspace.AddSiteMap(new SiteMapMetadata { UniqueName = "main_navigation" });
        workspace.AddSiteMap(new SiteMapMetadata { UniqueName = "MAIN_NAVIGATION" });
    }

    private static void AddDuplicateWebResource(Workspace workspace)
    {
        workspace.AddWebResource(new WebResourceMetadata { WebResourceId = "{66666666-6666-6666-6666-666666666666}", Name = "new_/script.js" });
        workspace.AddWebResource(new WebResourceMetadata { WebResourceId = "{66666666-6666-6666-6666-666666666666}", Name = "new_/script.js" });
    }

    private static void AddDuplicateWorkflow(Workspace workspace)
    {
        workspace.AddWorkflow(new WorkflowMetadata { WorkflowId = "{77777777-7777-7777-7777-777777777777}" });
        workspace.AddWorkflow(new WorkflowMetadata { WorkflowId = "{77777777-7777-7777-7777-777777777777}" });
    }

    private static void AddDuplicateFlowDefinition(Workspace workspace)
    {
        workspace.AddFlowDefinition(new FlowDefinitionMetadata { FilePath = "Workflows/test-flow.json" });
        workspace.AddFlowDefinition(new FlowDefinitionMetadata { FilePath = "workflows/TEST-FLOW.json" });
    }

    private static void AddDuplicateGenericComponent(Workspace workspace)
    {
        workspace.AddGenericComponent(new GenericComponentMetadata
        {
            ComponentTypeName = "Connector",
            FilePath = "Other/Connector.xml"
        });
        workspace.AddGenericComponent(new GenericComponentMetadata
        {
            ComponentTypeName = "Connector",
            FilePath = "other/connector.xml"
        });
    }
}
