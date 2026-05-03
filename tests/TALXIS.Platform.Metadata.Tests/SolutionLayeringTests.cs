using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionLayeringTests
{
    [Fact]
    public void LayerStack_TopWins()
    {
        var stack = new LayerStack { ComponentType = ComponentType.Entity, ComponentId = "account" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "ISV", Order = 1, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "Active", Order = 2, IsManaged = false });

        Assert.Equal(3, stack.Layers.Count);
        Assert.Equal("Active", stack.ActiveLayer!.SolutionName);
        Assert.Equal("Base", stack.BaseLayer!.SolutionName);
    }

    [Fact]
    public void LayerStack_RemoveLayer()
    {
        var stack = new LayerStack { ComponentType = ComponentType.Entity, ComponentId = "contact" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "ISV", Order = 1, IsManaged = true });

        var removed = stack.RemoveLayer("ISV");

        Assert.True(removed);
        Assert.Single(stack.Layers);
        Assert.Equal("Base", stack.Layers[0].SolutionName);
        Assert.False(stack.RemoveLayer("NonExistent"));
    }

    [Fact]
    public void LayerStack_ResolveTopWins_ReturnsTopComponent()
    {
        var baseForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Base Form") };
        var customForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Custom Form") };

        var stack = new LayerStack { ComponentType = ComponentType.SystemForm, ComponentId = "form-1" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true, Component = baseForm });
        stack.PushLayer(new ComponentLayer { SolutionName = "Active", Order = 1, IsManaged = false, Component = customForm });

        var resolved = stack.ResolveTopWins<FormMetadata>();
        Assert.NotNull(resolved);
        Assert.Equal("Custom Form", resolved!.DisplayName.Default);
    }

    [Fact]
    public void LayerStack_ResolveTopWins_ReturnsNull_WhenTopDeleted()
    {
        var entity = new EntityMetadata { LogicalName = "account" };

        var stack = new LayerStack { ComponentType = ComponentType.WebResource, ComponentId = "wr-1" };
        stack.PushLayer(new ComponentLayer
        {
            SolutionName = "Active", Order = 0, Component = entity,
            State = ComponentState.Deleted
        });

        Assert.Null(stack.ResolveTopWins<EntityMetadata>());
    }

    [Fact]
    public void SolutionLayerManager_ImportAndFind()
    {
        var mgr = new SolutionLayerManager();
        var accountEntity = new EntityMetadata { LogicalName = "account" };
        var mainForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Main") };

        var components = new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.Entity, "account", accountEntity),
            (ComponentType.SystemForm, "form-1", mainForm)
        };

        mgr.ImportSolutionLayer("MySolution", 1, true, components);

        var entityStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(entityStack);
        Assert.Single(entityStack!.Layers);
        Assert.Equal("MySolution", entityStack.Layers[0].SolutionName);
        Assert.True(entityStack.Layers[0].IsManaged);
        var resolvedEntity = entityStack.Layers[0].Component as EntityMetadata;
        Assert.NotNull(resolvedEntity);
        Assert.Equal("account", resolvedEntity!.LogicalName);

        var formStack = mgr.FindStack(ComponentType.SystemForm, "form-1");
        Assert.NotNull(formStack);
        var resolvedForm = formStack!.Layers[0].Component as FormMetadata;
        Assert.NotNull(resolvedForm);
        Assert.Equal("Main", resolvedForm!.DisplayName.Default);

        Assert.Null(mgr.FindStack(ComponentType.Entity, "nonexistent"));
    }

    [Fact]
    public void SolutionLayerManager_RemoveSolution()
    {
        var mgr = new SolutionLayerManager();

        var sol1Entity = new EntityMetadata { LogicalName = "account" };
        var sol1Attr = new StringAttributeMetadata { LogicalName = "name" };
        var sol2Entity = new EntityMetadata { LogicalName = "account" };

        mgr.ImportSolutionLayer("Sol1", 0, true, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.Entity, "account", sol1Entity),
            (ComponentType.Attribute, "name", sol1Attr)
        });
        mgr.ImportSolutionLayer("Sol2", 1, true, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.Entity, "account", sol2Entity)
        });

        mgr.RemoveSolutionLayers("Sol1");

        // account stack still exists (has Sol2 layer)
        var accountStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(accountStack);
        Assert.Single(accountStack!.Layers);
        Assert.Equal("Sol2", accountStack.Layers[0].SolutionName);

        // name stack was emptied and cleaned up
        Assert.Null(mgr.FindStack(ComponentType.Attribute, "name"));
        Assert.Single(mgr.AllStacks);
    }

    [Fact]
    public void SolutionLayerManager_GetOrCreate_ReturnsSameInstance()
    {
        var mgr = new SolutionLayerManager();

        var stack1 = mgr.GetOrCreateStack(ComponentType.Entity, "account");
        var stack2 = mgr.GetOrCreateStack(ComponentType.Entity, "account");

        Assert.Same(stack1, stack2);
    }

    [Fact]
    public void SolutionLayerManager_Resolve_TopWins_ForNonMergeableType()
    {
        var mgr = new SolutionLayerManager();
        var baseEntity = new EntityMetadata { LogicalName = "account" };
        var activeEntity = new EntityMetadata { LogicalName = "account", IsAuditEnabled = true };

        mgr.ImportSolutionLayer("Base", 0, true, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.Entity, "account", baseEntity)
        });
        mgr.ImportSolutionLayer("Active", 1, false, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.Entity, "account", activeEntity)
        });

        var stack = mgr.FindStack(ComponentType.Entity, "account")!;
        var resolved = mgr.Resolve(stack) as EntityMetadata;

        Assert.NotNull(resolved);
        Assert.True(resolved!.IsAuditEnabled);
    }

    [Fact]
    public void SolutionLayerManager_Resolve_UsesMerger_ForMergeableType()
    {
        var mgr = new SolutionLayerManager();
        mgr.RegisterMerger(new StubFormMerger());

        var baseForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Base") };
        var activeForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Active") };

        mgr.ImportSolutionLayer("Base", 0, true, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.SystemForm, "form-1", baseForm)
        });
        mgr.ImportSolutionLayer("Active", 1, false, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.SystemForm, "form-1", activeForm)
        });

        var stack = mgr.FindStack(ComponentType.SystemForm, "form-1")!;
        stack.RequiresMerge = true;

        var resolved = mgr.Resolve(stack) as FormMetadata;
        Assert.NotNull(resolved);
        Assert.Equal("Merged(2)", resolved!.DisplayName.Default);
    }

    [Fact]
    public void SolutionLayerManager_MarksKnownMergeableStacksAutomatically()
    {
        var mgr = new SolutionLayerManager();

        mgr.ImportSolutionLayer("Base", 0, true, new (ComponentType, string, MetadataBase?)[]
        {
            (ComponentType.SystemForm, "form-1", new FormMetadata { FormId = "form-1" }),
            (ComponentType.SiteMap, "site-map", new SiteMapMetadata { UniqueName = "site-map" }),
            (ComponentType.AppModule, "app", new AppModuleMetadata { UniqueName = "app" }),
            (ComponentType.RibbonCustomization, "account", new RibbonMetadata { EntityLogicalName = "account" }),
            (ComponentType.Entity, "account", new EntityMetadata { LogicalName = "account" })
        });

        Assert.True(mgr.FindStack(ComponentType.SystemForm, "form-1")!.RequiresMerge);
        Assert.True(mgr.FindStack(ComponentType.SiteMap, "site-map")!.RequiresMerge);
        Assert.True(mgr.FindStack(ComponentType.AppModule, "app")!.RequiresMerge);
        Assert.True(mgr.FindStack(ComponentType.RibbonCustomization, "account")!.RequiresMerge);
        Assert.False(mgr.FindStack(ComponentType.Entity, "account")!.RequiresMerge);
    }

    [Fact]
    public void Workspace_EnumerateLayerComponents_UsesStableLayerIdentities()
    {
        var workspace = new Workspace("/tmp/workspace");
        workspace.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contact",
            ReferencedEntity = "account",
            ReferencedAttribute = "accountid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        });
        workspace.AddForm(new FormMetadata { FormId = "form-1", EntityLogicalName = "account" });
        workspace.AddSiteMap(new SiteMapMetadata { UniqueName = "app_sitemap" });
        workspace.AddAppModule(new AppModuleMetadata { UniqueName = "app" });
        workspace.AddRibbon(new RibbonMetadata { EntityLogicalName = "account" });

        var components = workspace.EnumerateLayerComponents().ToArray();

        Assert.Contains(components, c => c.type == ComponentType.EntityRelationship && c.id == "account_contact");
        Assert.Contains(components, c => c.type == ComponentType.SystemForm && c.id == "form-1");
        Assert.Contains(components, c => c.type == ComponentType.SiteMap && c.id == "app_sitemap");
        Assert.Contains(components, c => c.type == ComponentType.AppModule && c.id == "app");
        Assert.Contains(components, c => c.type == ComponentType.RibbonCustomization && c.id == "account");
    }

    /// <summary>Stub merger that returns a FormMetadata indicating how many layers were merged.</summary>
    private sealed class StubFormMerger : IComponentMerger
    {
        public ComponentType ComponentType => ComponentType.SystemForm;

        public MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers)
        {
            return new FormMetadata
            {
                FormId = "merged",
                DisplayName = new Label($"Merged({layers.Count})")
            };
        }
    }
}
