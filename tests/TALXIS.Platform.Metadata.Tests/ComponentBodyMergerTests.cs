using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentBodyMergerTests
{
    [Fact]
    public void AppModuleMerger_MergesBodyByRegisteredComponentKeys()
    {
        var merger = new AppModuleMerger();
        var baseApp = new AppModuleMetadata { UniqueName = "test_app", Body = Node("AppModule",
            Node("AppModuleComponents",
                Node("AppModuleComponent", new Dictionary<string, string> { ["type"] = "1", ["schemaName"] = "account", ["ordinalvalue"] = "20" }))) };
        var added = Node("AppModuleComponent", new Dictionary<string, string> { ["type"] = "1", ["schemaName"] = "contact", ["ordinalvalue"] = "10" });
        added.Action = MergeAction.Added;
        var layerApp = new AppModuleMetadata { UniqueName = "test_app", Body = Node("AppModule", Node("AppModuleComponents", added)) };

        var result = Assert.IsType<AppModuleMetadata>(merger.Merge(Layers(baseApp, layerApp)));

        var components = FindAll(result.Body!, "AppModuleComponent");
        Assert.Equal(["contact", "account"], components.Select(c => c.GetAttribute("schemaName")!).ToArray());
        Assert.All(components, c => Assert.Null(c.Action));
    }

    [Fact]
    public void SiteMapMerger_MergesBodyBySiteMapKeysAndOrdinalValue()
    {
        var merger = new SiteMapMerger();
        var baseSiteMap = new SiteMapMetadata { UniqueName = "test_sitemap", Body = Node("AppModuleSiteMap",
            Node("SiteMap",
                Node("Area", new Dictionary<string, string> { ["Id"] = "main" },
                    Node("Group", new Dictionary<string, string> { ["Id"] = "core" },
                        Node("SubArea", new Dictionary<string, string> { ["Id"] = "account", ["ordinalvalue"] = "20" }))))) };
        var added = Node("SubArea", new Dictionary<string, string> { ["Id"] = "contact", ["ordinalvalue"] = "10" });
        added.Action = MergeAction.Added;
        var layerSiteMap = new SiteMapMetadata { UniqueName = "test_sitemap", Body = Node("AppModuleSiteMap",
            Node("SiteMap",
                Node("Area", new Dictionary<string, string> { ["Id"] = "main" },
                    Node("Group", new Dictionary<string, string> { ["Id"] = "core" }, added)))) };

        var result = Assert.IsType<SiteMapMetadata>(merger.Merge(Layers(baseSiteMap, layerSiteMap)));

        var subAreas = FindAll(result.Body!, "SubArea");
        Assert.Equal(["contact", "account"], subAreas.Select(c => c.GetAttribute("Id")!).ToArray());
        Assert.All(subAreas, c => Assert.Null(c.Action));
    }

    [Fact]
    public void RibbonMerger_MergesRibbonDiffXmlByRibbonKeys()
    {
        var merger = new RibbonMerger();
        var baseRibbon = new RibbonMetadata { EntityLogicalName = "account", Body = Node("RibbonDiffXml",
            Node("CustomActions",
                Node("CustomAction", new Dictionary<string, string> { ["Id"] = "account.base.Action", ["Sequence"] = "20" })),
            Node("CommandDefinitions",
                Node("CommandDefinition", new Dictionary<string, string> { ["Id"] = "account.base.Command" }))) };
        var addedAction = Node("CustomAction", new Dictionary<string, string> { ["Id"] = "account.added.Action", ["Sequence"] = "10" });
        addedAction.Action = MergeAction.Added;
        var addedEnableRules = Node("EnableRules",
            Node("EnableRule", new Dictionary<string, string> { ["Id"] = "account.EnableRule" }));
        addedEnableRules.Action = MergeAction.Added;
        var modifiedCommand = Node("CommandDefinition", new Dictionary<string, string> { ["Id"] = "account.base.Command" }, addedEnableRules);
        modifiedCommand.Action = MergeAction.Modified;
        var layerRibbon = new RibbonMetadata { EntityLogicalName = "account", Body = Node("RibbonDiffXml",
            Node("CustomActions", addedAction),
            Node("CommandDefinitions", modifiedCommand)) };

        var result = Assert.IsType<RibbonMetadata>(merger.Merge(Layers(baseRibbon, layerRibbon)));

        var actions = FindAll(result.Body!, "CustomAction");
        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, a => a.GetAttribute("Id") == "account.added.Action" && a.Action == null);
        var command = Assert.Single(FindAll(result.Body!, "CommandDefinition"));
        Assert.NotNull(FindAll(command, "EnableRule").SingleOrDefault(r => r.GetAttribute("Id") == "account.EnableRule"));
        Assert.Null(command.Action);
    }

    private static IReadOnlyList<ComponentLayer> Layers(MetadataBase baseComponent, MetadataBase topComponent) =>
    [
        new ComponentLayer { SolutionUniqueName = "base", Order = 1, IsManaged = true, Component = baseComponent },
        new ComponentLayer { SolutionUniqueName = "top", Order = 2, IsManaged = false, Component = topComponent }
    ];

    private static MergeableNode Node(string name, params MergeableNode[] children) => Node(name, null, children);

    private static MergeableNode Node(string name, Dictionary<string, string>? attributes, params MergeableNode[] children)
    {
        var node = new MergeableNode { Name = name };
        if (attributes != null)
        {
            foreach (var attribute in attributes)
                node.SetAttribute(attribute.Key, attribute.Value);
        }

        foreach (var child in children)
            node.AddChild(child);
        return node;
    }

    private static List<MergeableNode> FindAll(MergeableNode root, string name)
    {
        var result = new List<MergeableNode>();
        Visit(root);
        return result;

        void Visit(MergeableNode node)
        {
            if (node.Name == name) result.Add(node);
            foreach (var child in node.Children) Visit(child);
        }
    }
}
