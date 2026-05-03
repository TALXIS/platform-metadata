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
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "ISV", Order = 1, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "Active", Order = 2, IsManaged = false, LayerKind = SolutionLayerKind.Active });

        Assert.Equal(3, stack.Layers.Count);
        Assert.Equal("Active", stack.ActiveLayer!.SolutionUniqueName);
        Assert.Equal("Base", stack.BaseLayer!.SolutionUniqueName);
    }

    [Fact]
    public void LayerStack_RemoveLayer()
    {
        var stack = new LayerStack { ComponentType = ComponentType.Entity, ComponentId = "contact" };
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "ISV", Order = 1, IsManaged = true });

        var removed = stack.RemoveLayer("ISV");

        Assert.True(removed);
        Assert.Single(stack.Layers);
        Assert.Equal("Base", stack.Layers[0].SolutionUniqueName);
        Assert.False(stack.RemoveLayer("NonExistent"));
    }

    [Fact]
    public void LayerStack_ResolveTopWins_ReturnsTopComponent()
    {
        var baseForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Base Form") };
        var customForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Custom Form") };

        var stack = new LayerStack { ComponentType = ComponentType.SystemForm, ComponentId = "form-1" };
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "Base", Order = 0, IsManaged = true, Component = baseForm });
        stack.PushLayer(new ComponentLayer { SolutionUniqueName = "Active", Order = 1, IsManaged = false, LayerKind = SolutionLayerKind.Active, Component = customForm });

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
            SolutionUniqueName = "Active", Order = 0, Component = entity,
            LayerKind = SolutionLayerKind.Active,
            State = ComponentState.Delete
        });

        Assert.Null(stack.ResolveTopWins<EntityMetadata>());
    }

    [Fact]
    public void SolutionLayerManager_ImportAndFind()
    {
        var mgr = new SolutionLayerManager();
        var accountEntity = new EntityMetadata { LogicalName = "account" };
        var mainForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Main") };

        var components = new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", accountEntity),
            new LayerComponentDescriptor(ComponentType.SystemForm, "form-1", mainForm)
        };

        mgr.ImportSolutionLayer("MySolution", 1, true, components);

        var entityStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(entityStack);
        Assert.Single(entityStack!.Layers);
        Assert.Equal("MySolution", entityStack.Layers[0].SolutionUniqueName);
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

        mgr.ImportSolutionLayer("Sol1", 0, true, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", sol1Entity),
            new LayerComponentDescriptor(ComponentType.Attribute, "name", sol1Attr)
        });
        mgr.ImportSolutionLayer("Sol2", 1, true, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", sol2Entity)
        });

        mgr.RemoveSolutionLayers("Sol1");

        // account stack still exists (has Sol2 layer)
        var accountStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(accountStack);
        Assert.Single(accountStack!.Layers);
        Assert.Equal("Sol2", accountStack.Layers[0].SolutionUniqueName);

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

        mgr.ImportSolutionLayer("Base", 0, true, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", baseEntity)
        });
        mgr.ImportSolutionLayer("Active", 1, false, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", activeEntity)
        });

        var stack = mgr.FindStack(ComponentType.Entity, "account")!;
        var resolved = mgr.Resolve(stack) as EntityMetadata;

        Assert.NotNull(resolved);
        Assert.True(resolved!.IsAuditEnabled);
    }

    [Fact]
    public void SolutionLayerManager_ActiveLayer_SortsAboveManagedLayer()
    {
        var mgr = new SolutionLayerManager();
        var managedSolution = new Solution { UniqueName = "ManagedBase", IsManaged = true };
        var unmanagedSolution = new Solution { UniqueName = "UnmanagedEdits", IsManaged = false };
        var managedEntity = new EntityMetadata { LogicalName = "account", DisplayName = new Label("Managed") };
        var activeEntity = new EntityMetadata { LogicalName = "account", DisplayName = new Label("Active") };

        mgr.ImportManagedLayer(managedSolution, 100, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", managedEntity)
        });
        mgr.ImportActiveLayerSnapshot(unmanagedSolution, 0, new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", activeEntity)
        });

        var stack = mgr.FindStack(ComponentType.Entity, "account")!;

        Assert.Equal("ManagedBase", stack.BaseLayer!.SolutionUniqueName);
        Assert.Equal(SolutionLayerManager.ActiveSolutionName, stack.ActiveLayer!.SolutionUniqueName);
        Assert.Equal("UnmanagedEdits", stack.ActiveLayer.SourceSolutionUniqueName);
        Assert.Equal("Active", ((EntityMetadata)mgr.Resolve(stack)!).DisplayName.Default);
    }

    [Fact]
    public void Workspace_AddSolution_RejectsDuplicateUniqueName()
    {
        var workspace = new Workspace("/tmp/workspace");
        workspace.AddSolution(new Solution { UniqueName = "Core" });

        var ex = Assert.Throws<InvalidOperationException>(() => workspace.AddSolution(new Solution { UniqueName = "core" }));

        Assert.Contains("solution", ex.Message);
    }

    [Fact]
    public void Workspace_RegisterSolutionSource_TracksMembershipAndUnmanagedActiveSnapshotSeparately()
    {
        var workspace = new Workspace("/tmp/workspace");
        var solution = new Solution { UniqueName = "UnmanagedUi", IsManaged = false };
        solution.AddRootComponent(new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = "account",
            BehaviorOption = RootComponentBehavior.DoNotIncludeSubcomponents
        });
        workspace.AddSolution(solution);

        workspace.RegisterSolutionSource(solution, 7, "/tmp/workspace", new[]
        {
            new LayerComponentDescriptor(ComponentType.Entity, "account", new EntityMetadata { LogicalName = "account" }, "Entity:account")
        });

        var membership = Assert.Single(workspace.SolutionComponents);
        Assert.Equal("UnmanagedUi", membership.SolutionUniqueName);
        Assert.Equal(ComponentType.Entity, membership.Component.Type);
        Assert.Equal("account", membership.Component.ObjectId);

        var snapshot = Assert.Single(workspace.ComponentSources);
        Assert.Equal("UnmanagedUi", snapshot.SourceSolutionUniqueName);
        Assert.False(snapshot.IsManaged);

        var layer = workspace.Layers.FindStack(ComponentType.Entity, "account")!.ActiveLayer!;
        Assert.Equal(SolutionLayerManager.ActiveSolutionName, layer.SolutionUniqueName);
        Assert.Equal("UnmanagedUi", layer.SourceSolutionUniqueName);
        Assert.Equal(SolutionLayerKind.Active, layer.LayerKind);

        Assert.True(workspace.RemoveSolution("UnmanagedUi"));
        Assert.Empty(workspace.SolutionComponents);
        Assert.Empty(workspace.ComponentSources);
        Assert.Null(workspace.Layers.FindStack(ComponentType.Entity, "account"));
    }

    [Fact]
    public void Workspace_RegisterSolutionSource_RetainsMultipleUnmanagedActiveSnapshots()
    {
        var workspace = new Workspace("/tmp/workspace");
        var firstSolution = new Solution { UniqueName = "UnmanagedOne", IsManaged = false };
        var secondSolution = new Solution { UniqueName = "UnmanagedTwo", IsManaged = false };
        workspace.AddSolution(firstSolution);
        workspace.AddSolution(secondSolution);

        workspace.RegisterSolutionSource(firstSolution, 0, "/tmp/one", new[]
        {
            new LayerComponentDescriptor(
                ComponentType.Entity,
                "account",
                new EntityMetadata { LogicalName = "account", DisplayName = new Label("One") },
                "Entity:account")
        });
        workspace.RegisterSolutionSource(firstSolution, 1, "/tmp/one", new[]
        {
            new LayerComponentDescriptor(
                ComponentType.Entity,
                "account",
                new EntityMetadata { LogicalName = "account", DisplayName = new Label("One Again") },
                "Entity:account")
        });
        workspace.RegisterSolutionSource(secondSolution, 1, "/tmp/two", new[]
        {
            new LayerComponentDescriptor(
                ComponentType.Entity,
                "account",
                new EntityMetadata { LogicalName = "account", DisplayName = new Label("Two") },
                "Entity:account")
        });

        var stack = workspace.Layers.FindStack(ComponentType.Entity, "account")!;

        Assert.Equal(3, stack.Layers.Count);
        Assert.All(stack.Layers, layer => Assert.Equal(SolutionLayerKind.Active, layer.LayerKind));
        Assert.Equal("UnmanagedTwo", stack.ActiveLayer!.SourceSolutionUniqueName);
        Assert.Equal("Two", ((EntityMetadata)workspace.Layers.Resolve(stack)!).DisplayName.Default);

        Assert.True(workspace.RemoveSolution("UnmanagedTwo"));
        stack = workspace.Layers.FindStack(ComponentType.Entity, "account")!;

        Assert.Equal(2, stack.Layers.Count);
        Assert.Equal("UnmanagedOne", stack.ActiveLayer!.SourceSolutionUniqueName);
        Assert.Equal("One Again", ((EntityMetadata)workspace.Layers.Resolve(stack)!).DisplayName.Default);

        Assert.True(workspace.RemoveSolution("UnmanagedOne"));
        Assert.Null(workspace.Layers.FindStack(ComponentType.Entity, "account"));
    }

    [Fact]
    public void SolutionLayerManager_Resolve_UsesMerger_ForMergeableType()
    {
        var mgr = new SolutionLayerManager();
        mgr.RegisterMerger(new StubFormMerger());

        var baseForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Base") };
        var activeForm = new FormMetadata { FormId = "form-1", DisplayName = new Label("Active") };

        mgr.ImportSolutionLayer("Base", 0, true, new[]
        {
            new LayerComponentDescriptor(ComponentType.SystemForm, "form-1", baseForm)
        });
        mgr.ImportSolutionLayer("Active", 1, false, new[]
        {
            new LayerComponentDescriptor(ComponentType.SystemForm, "form-1", activeForm)
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

        mgr.ImportSolutionLayer("Base", 0, true, new[]
        {
            new LayerComponentDescriptor(ComponentType.SystemForm, "form-1", new FormMetadata { FormId = "form-1" }),
            new LayerComponentDescriptor(ComponentType.SiteMap, "site-map", new SiteMapMetadata { UniqueName = "site-map" }),
            new LayerComponentDescriptor(ComponentType.AppModule, "app", new AppModuleMetadata { UniqueName = "app" }),
            new LayerComponentDescriptor(ComponentType.RibbonCustomization, "account", new RibbonMetadata { EntityLogicalName = "account" }),
            new LayerComponentDescriptor(ComponentType.Entity, "account", new EntityMetadata { LogicalName = "account" })
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

        Assert.Contains(components, c => c.Type == ComponentType.EntityRelationship && c.Id == "account_contact");
        Assert.Contains(components, c => c.Type == ComponentType.SystemForm && c.Id == "form-1");
        Assert.Contains(components, c => c.Type == ComponentType.SiteMap && c.Id == "app_sitemap");
        Assert.Contains(components, c => c.Type == ComponentType.AppModule && c.Id == "app");
        Assert.Contains(components, c => c.Type == ComponentType.RibbonCustomization && c.Id == "account");
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
