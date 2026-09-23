using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class WorkspaceComponentAccessTests
{
    public static IEnumerable<object[]> Components() => new[]
    {
        new object[] { new EntityMetadata { LogicalName = "ntg_order" }, ComponentType.Entity, "ntg_order", "Entity:ntg_order" },
        new object[] { new OptionSetMetadata { Name = "ntg_status" }, ComponentType.OptionSet, "ntg_status", "OptionSet:ntg_status" },
        new object[] { NewRelationship("ntg_order_line"), ComponentType.EntityRelationship, "ntg_order_line", "Relationship:ntg_order_line" },
        new object[] { new FormMetadata { FormId = "form-1", EntityLogicalName = "ntg_order" }, ComponentType.SystemForm, "form-1", "Form:ntg_order:form-1" },
        new object[] { new SavedQueryMetadata { SavedQueryId = "view-1", EntityLogicalName = "ntg_order" }, ComponentType.SavedQuery, "view-1", "View:ntg_order:view-1" },
        new object[] { new PluginAssemblyMetadata { PluginAssemblyId = "pa-1", Name = "Ntg.Plugins" }, ComponentType.PluginAssembly, "pa-1", "PluginAssembly:Ntg.Plugins" },
        new object[] { new SdkMessageProcessingStepMetadata { SdkMessageProcessingStepId = "step-1" }, ComponentType.SdkMessageProcessingStep, "step-1", "Step:step-1" },
        new object[] { new SecurityRoleMetadata { RoleId = "role-1", Name = "Admin" }, ComponentType.Role, "role-1", "Role:role-1" },
        new object[] { new AppModuleMetadata { UniqueName = "ntg_app" }, ComponentType.AppModule, "ntg_app", "AppModule:ntg_app" },
        new object[] { new SiteMapMetadata { UniqueName = "ntg_sitemap" }, ComponentType.SiteMap, "ntg_sitemap", "SiteMap:ntg_sitemap" },
        new object[] { new WebResourceMetadata { WebResourceId = "wr-1", Name = "ntg_/script.js" }, ComponentType.WebResource, "wr-1", "WebResource:ntg_/script.js" },
        new object[] { new WorkflowMetadata { WorkflowId = "wf-1" }, ComponentType.Workflow, "wf-1", "Workflow:wf-1" },
        new object[] { new RibbonMetadata { EntityLogicalName = "ntg_order" }, ComponentType.RibbonCustomization, "ntg_order", "Ribbon:ntg_order" },
        new object[] { new RibbonMetadata(), ComponentType.RibbonCustomization, "global", "Ribbon:global" },
        new object[] { new FlowDefinitionMetadata { FilePath = "Workflows/flow.json" }, ComponentType.GenericComponent, "Workflows/flow.json", "FlowDefinition:Workflows/flow.json" },
        new object[] { new GenericComponentMetadata { ComponentTypeName = "ConnectionRole", FilePath = "Other/ConnectionRoles.xml" }, ComponentType.GenericComponent, "Other/ConnectionRoles.xml", "Generic:Other/ConnectionRoles.xml" },
    };

    [Theory]
    [MemberData(nameof(Components))]
    public void Identity_AndDocumentKey_DeriveFromComponentKeys(ISolutionComponent component, ComponentType type, string objectId, string documentKey)
    {
        Assert.Equal(type, component.Identity.Type);
        Assert.Equal(objectId, component.Identity.ObjectId);
        Assert.Equal(documentKey, component.DocumentKey);
        Assert.NotNull(ComponentDefinitionRegistry.GetByType(component.Identity.Type));
    }

    [Fact]
    public void Components_EnumerateEveryTypedList_InLayerDescriptorOrder()
    {
        var workspace = CreateFullWorkspace();

        var components = workspace.Components.ToArray();
        var descriptors = workspace.EnumerateLayerComponents().ToArray();

        Assert.Equal(15, components.Length);
        Assert.Equal(components.Length, descriptors.Length);
        for (var i = 0; i < components.Length; i++)
        {
            Assert.Equal(components[i].Identity, descriptors[i].Identity);
            Assert.Equal(components[i].DocumentKey, descriptors[i].SourceDocumentKey);
            Assert.Same(components[i], descriptors[i].Metadata);
        }
    }

    [Fact]
    public void GetComponent_FindsByIdentity_IgnoringObjectIdCase()
    {
        var workspace = CreateFullWorkspace();

        var component = workspace.GetComponent(ComponentType.Entity, "NTG_ORDER");

        Assert.NotNull(component);
        Assert.Same(workspace.Entities[0], component!.Metadata);
        Assert.Equal(new ComponentIdentity(ComponentType.Entity, "ntg_order"), component.Identity);
        Assert.Null(workspace.GetComponent(ComponentType.Entity, "missing"));
        Assert.Null(workspace.GetComponent(ComponentType.SystemForm, "ntg_order"));
    }

    [Fact]
    public void GetComponent_WithoutLayers_HasEmptySurroundings()
    {
        var workspace = CreateFullWorkspace();

        var component = workspace.GetComponent(ComponentType.Entity, "ntg_order")!;

        Assert.Empty(component.Layers);
        Assert.Null(component.ActiveState);
        Assert.Empty(component.Memberships);
        Assert.Empty(component.Snapshots);
    }

    [Fact]
    public void GetComponent_AfterRegisteringSolutionSource_ExposesLayersMembershipsAndSnapshots()
    {
        var workspace = CreateFullWorkspace();
        var solution = new Solution { UniqueName = "Solutions.DataModel" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "ntg_order", Behavior = 0 });
        workspace.AddSolution(solution);
        workspace.RegisterSolutionSource(solution, 0, "/tmp/datamodel", workspace.EnumerateLayerComponents());

        var entity = workspace.GetComponent(ComponentType.Entity, "ntg_order")!;
        var form = workspace.GetComponent(ComponentType.SystemForm, "form-1")!;

        Assert.Single(entity.Layers);
        Assert.Equal(SolutionLayerKind.Active, entity.Layers[0].LayerKind);
        Assert.Equal("Solutions.DataModel", entity.Layers[0].SourceSolutionUniqueName);
        Assert.Same(entity.Metadata, entity.ActiveState);
        Assert.Single(entity.Memberships);
        Assert.Equal("Solutions.DataModel", entity.Memberships[0].SolutionUniqueName);
        Assert.Single(entity.Snapshots);
        Assert.Equal("/tmp/datamodel", entity.Snapshots[0].SourceRootPath);
        Assert.Empty(form.Memberships);
        Assert.Single(form.Layers);
    }

    [Fact]
    public void ActiveState_TopWins_PrefersActiveLayerOverManagedRegardlessOfOrder()
    {
        var workspace = new Workspace("/tmp/access");
        var vendorEntity = new EntityMetadata { LogicalName = "ntg_order", DisplayName = new Label("Vendor Order") };
        var myEntity = new EntityMetadata { LogicalName = "ntg_order", DisplayName = new Label("My Order") };
        workspace.AddEntity(myEntity);
        Register(workspace, "Solutions.DataModel", isManaged: false, order: 0, Describe(myEntity));
        Register(workspace, "Vendor.Base", isManaged: true, order: 100, Describe(vendorEntity));

        var component = workspace.GetComponent(ComponentType.Entity, "ntg_order")!;

        Assert.Equal(2, component.Layers.Count);
        Assert.Equal("Vendor.Base", component.Layers[0].LayerSolutionUniqueName);
        Assert.Equal(SolutionLayerManager.ActiveSolutionName, component.Layers[1].LayerSolutionUniqueName);
        Assert.Same(myEntity, component.ActiveState);
    }

    [Fact]
    public void ActiveState_MergesFormBodiesAcrossLayers()
    {
        var workspace = new Workspace("/tmp/access");
        var vendorForm = new FormMetadata { FormId = "form-1", EntityLogicalName = "ntg_order", Body = FormWithTab("general", null) };
        var myForm = new FormMetadata { FormId = "form-1", EntityLogicalName = "ntg_order", Body = FormWithTab("extra", MergeAction.Added) };
        workspace.AddForm(myForm);
        Register(workspace, "Vendor.Base", isManaged: true, order: 0, Describe(vendorForm));
        Register(workspace, "Solutions.UI", isManaged: false, order: 1, Describe(myForm));

        var merged = Assert.IsType<FormMetadata>(workspace.GetComponent(ComponentType.SystemForm, "form-1")!.ActiveState);

        Assert.NotSame(vendorForm, merged);
        Assert.NotSame(myForm, merged);
        var tabIds = merged.Body!.Descendants().Where(n => n.Name == "tab").Select(n => n.GetAttribute("id")).ToArray();
        Assert.Equal(new[] { "general", "extra" }, tabIds);
    }

    [Fact]
    public void ActiveState_IsNull_WhenTopLayerIsDeleted()
    {
        var workspace = new Workspace("/tmp/access");
        var entity = new EntityMetadata { LogicalName = "ntg_order" };
        workspace.AddEntity(entity);
        Register(workspace, "Solutions.DataModel", isManaged: false, order: 0, Describe(entity));
        var component = workspace.GetComponent(ComponentType.Entity, "ntg_order")!;

        component.Layers[0].State = ComponentState.Delete;

        Assert.Null(component.ActiveState);
    }

    [Fact]
    public void Reader_KeysOriginalDocumentsByComponentDocumentKey_AndRemoveClearsThem()
    {
        var root = NewTempDir();
        try
        {
            var dir = WriteProject(root, "ContractSolution", managed: false, displayName: "Test Entity");

            var workspace = new XmlWorkspaceReader().Load(dir);
            var component = workspace.GetComponent(ComponentType.Entity, "test_entity")!;
            var entity = Assert.IsType<EntityMetadata>(component.Metadata);

            Assert.True(workspace.OriginalDocuments().ContainsKey(entity.DocumentKey));
            Assert.Single(component.Memberships);
            Assert.Same(entity, component.ActiveState);

            Assert.True(workspace.RemoveEntity("test_entity"));
            Assert.False(workspace.OriginalDocuments().ContainsKey(entity.DocumentKey));
            Assert.Null(workspace.GetComponent(ComponentType.Entity, "test_entity"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadMany_ThenGetComponent_ResolvesUnmanagedProjectOverManaged()
    {
        var root = NewTempDir();
        try
        {
            var vendor = WriteProject(root, "Vendor.Base", managed: true, displayName: "Vendor Order");
            var mine = WriteProject(root, "Solutions.DataModel", managed: false, displayName: "My Order");

            var workspace = new XmlWorkspaceReader().LoadMany(new[]
            {
                new SolutionWorkspaceSource(vendor, importOrder: 0),
                new SolutionWorkspaceSource(mine, importOrder: 1)
            });
            var component = workspace.GetComponent(ComponentType.Entity, "test_entity")!;

            Assert.Equal(2, component.Layers.Count);
            Assert.Equal(2, component.Snapshots.Count);
            Assert.Equal("My Order", Assert.IsType<EntityMetadata>(component.ActiveState).DisplayName.Default);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Workspace CreateFullWorkspace()
    {
        var workspace = new Workspace("/tmp/access");
        workspace.AddEntity(new EntityMetadata { LogicalName = "ntg_order" });
        workspace.AddGlobalOptionSet(new OptionSetMetadata { Name = "ntg_status" });
        workspace.AddRelationship(NewRelationship("ntg_order_line"));
        workspace.AddForm(new FormMetadata { FormId = "form-1", EntityLogicalName = "ntg_order" });
        workspace.AddView(new SavedQueryMetadata { SavedQueryId = "view-1", EntityLogicalName = "ntg_order" });
        workspace.AddPluginAssembly(new PluginAssemblyMetadata { PluginAssemblyId = "pa-1", Name = "Ntg.Plugins" });
        workspace.AddSdkMessageProcessingStep(new SdkMessageProcessingStepMetadata { SdkMessageProcessingStepId = "step-1" });
        workspace.AddSecurityRole(new SecurityRoleMetadata { RoleId = "role-1", Name = "Admin" });
        workspace.AddAppModule(new AppModuleMetadata { UniqueName = "ntg_app" });
        workspace.AddSiteMap(new SiteMapMetadata { UniqueName = "ntg_sitemap" });
        workspace.AddWebResource(new WebResourceMetadata { WebResourceId = "wr-1", Name = "ntg_/script.js" });
        workspace.AddWorkflow(new WorkflowMetadata { WorkflowId = "wf-1" });
        workspace.AddRibbon(new RibbonMetadata { EntityLogicalName = "ntg_order" });
        workspace.AddFlowDefinition(new FlowDefinitionMetadata { FilePath = "Workflows/flow.json" });
        workspace.AddGenericComponent(new GenericComponentMetadata { ComponentTypeName = "ConnectionRole", FilePath = "Other/ConnectionRoles.xml" });
        return workspace;
    }

    private static void Register(Workspace workspace, string solutionName, bool isManaged, int order, params LayerComponentDescriptor[] components)
    {
        var solution = new Solution { UniqueName = solutionName, IsManaged = isManaged };
        workspace.AddSolution(solution);
        workspace.RegisterSolutionSource(solution, order, $"/tmp/{solutionName}", components);
    }

    private static LayerComponentDescriptor Describe(ISolutionComponent component) =>
        new(component.Identity, (MetadataBase)component, component.DocumentKey);

    private static OneToManyRelationshipMetadata NewRelationship(string schemaName) => new()
    {
        SchemaName = schemaName,
        ReferencedEntity = "ntg_order",
        ReferencedAttribute = "ntg_orderid",
        ReferencingEntity = "ntg_line",
        ReferencingAttribute = "ntg_orderid"
    };

    private static MergeableNode FormWithTab(string tabId, MergeAction? action)
    {
        var tab = new MergeableNode { Name = "tab", Action = action };
        tab.SetAttribute("id", tabId);
        var tabs = new MergeableNode { Name = "tabs" };
        tabs.AddChild(tab);
        var form = new MergeableNode { Name = "form" };
        form.AddChild(tabs);
        return form;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"component-access-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteProject(string root, string uniqueName, bool managed, string displayName)
    {
        var dir = Path.Combine(root, uniqueName);
        Directory.CreateDirectory(Path.Combine(dir, "Other"));
        Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
        File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml version="9.1">
              <SolutionManifest>
                <UniqueName>{uniqueName}</UniqueName>
                <Version>1.0</Version>
                <Managed>{(managed ? 1 : 0)}</Managed>
                <Publisher>
                  <UniqueName>test</UniqueName>
                  <CustomizationPrefix>test</CustomizationPrefix>
                </Publisher>
                <RootComponents>
                  <RootComponent type="1" schemaName="test_entity" behavior="0" />
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);
        File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity>
              <EntityInfo>
                <entity Name="test_entity">
                  <EntitySetName>test_entities</EntitySetName>
                  <LocalizedNames><LocalizedName description="{displayName}" languagecode="1033" /></LocalizedNames>
                  <LocalizedCollectionNames><LocalizedCollectionName description="{displayName}s" languagecode="1033" /></LocalizedCollectionNames>
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """);
        return dir;
    }
}
