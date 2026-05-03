using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Tests;

public class TreeMergeEngineTests
{
    private static MergeableNode BuildBaseForm()
    {
        // Mirrors the BaseFormXml from FormXmlMergeTests
        var root = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs",
                        Tab("{00000000-0000-0000-0000-000000000001}", "general",
                            Node("columns",
                                Node("column", new Dictionary<string, string> { ["width"] = "100%" },
                                    Node("sections",
                                        Section("{00000000-0000-0000-0000-000000000010}", "section1",
                                            Node("rows",
                                                Node("row",
                                                    Cell("{00000000-0000-0000-0000-000000000100}",
                                                        Control("name", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", "fullname"))))),
                                        Section("{00000000-0000-0000-0000-000000000011}", "section2",
                                            Node("rows",
                                                Node("row",
                                                    Cell("{00000000-0000-0000-0000-000000000101}",
                                                        Control("email", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", "emailaddress1")))))))))
                    ))));

        return root;
    }

    [Fact]
    public void Merge_AddedTab()
    {
        var baseTree = BuildBaseForm();
        var layer = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs",
                        Tab("{00000000-0000-0000-0000-000000000002}", "details",
                            Node("columns",
                                Node("column", new Dictionary<string, string> { ["width"] = "100%" },
                                    Node("sections",
                                        Section("{00000000-0000-0000-0000-000000000020}", "detailsSection",
                                            Node("rows",
                                                Node("row",
                                                    Cell("{00000000-0000-0000-0000-000000000200}",
                                                        Control("phone", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", "telephone1")))))))),
                            MergeAction.Added)))));

        var result = TreeMergeEngine.Merge(baseTree, layer);

        var tabs = FindAll(result, "tab");
        Assert.Equal(2, tabs.Count);
        Assert.Equal("{00000000-0000-0000-0000-000000000001}", tabs[0].GetAttribute("id"));
        Assert.Equal("{00000000-0000-0000-0000-000000000002}", tabs[1].GetAttribute("id"));
        Assert.Null(tabs[1].Action);
    }

    [Fact]
    public void Merge_RemovedSection()
    {
        var baseTree = BuildBaseForm();
        var layer = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs",
                        Node("tab", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000001}" },
                            Node("columns",
                                Node("column", new Dictionary<string, string> { ["width"] = "100%" },
                                    Node("sections",
                                        SectionShell("{00000000-0000-0000-0000-000000000011}", "section2", MergeAction.Removed)))))))));

        var result = TreeMergeEngine.Merge(baseTree, layer);

        var sections = FindAll(result, "section");
        Assert.Single(sections);
        Assert.Equal("{00000000-0000-0000-0000-000000000010}", sections[0].GetAttribute("id"));
    }

    [Fact]
    public void Merge_ModifiedControl()
    {
        var baseTree = BuildBaseForm();
        var layer = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs",
                        Node("tab", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000001}" },
                            Node("columns",
                                Node("column", new Dictionary<string, string> { ["width"] = "100%" },
                                    Node("sections",
                                        Node("section", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000010}" },
                                            Node("rows",
                                                Node("row",
                                                    Node("cell", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000100}" },
                                                        ControlModified("name", "fullname", "true")))))))))))));

        var result = TreeMergeEngine.Merge(baseTree, layer);

        var control = FindAll(result, "control").First(c => c.GetAttribute("id") == "name");
        Assert.Equal("true", control.GetAttribute("disabled"));
        Assert.Equal("{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", control.GetAttribute("classid"));
        Assert.Null(control.Action);
    }

    [Fact]
    public void Merge_AddedRow()
    {
        var baseTree = BuildBaseForm();
        var addedRow = Node("row",
            Cell("{00000000-0000-0000-0000-000000000102}",
                Control("jobtitle", "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}", "jobtitle")));
        addedRow.Action = MergeAction.Added;

        var layer = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs",
                        Node("tab", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000001}" },
                            Node("columns",
                                Node("column", new Dictionary<string, string> { ["width"] = "100%" },
                                    Node("sections",
                                        Node("section", new Dictionary<string, string> { ["id"] = "{00000000-0000-0000-0000-000000000010}" },
                                            Node("rows", addedRow))))))))));

        var result = TreeMergeEngine.Merge(baseTree, layer);

        var section = FindAll(result, "section").First(s => s.GetAttribute("id") == "{00000000-0000-0000-0000-000000000010}");
        var rows = FindAll(section, "row");
        Assert.Equal(2, rows.Count);
        var controls = FindAll(rows[1], "control");
        Assert.NotEmpty(controls);
        Assert.Equal("jobtitle", controls[0].GetAttribute("datafieldname"));
    }

    [Fact]
    public void Merge_PreservesUnchanged()
    {
        var baseTree = BuildBaseForm();
        var layer = Node("forms",
            Node("systemform",
                Node("form",
                    Node("tabs"))));

        var result = TreeMergeEngine.Merge(baseTree, layer);

        Assert.Single(FindAll(result, "tab"));
        Assert.Equal(2, FindAll(result, "section").Count);
        Assert.Equal(2, FindAll(result, "control").Count);
    }

    [Fact]
    public void ComputeDiff_DetectsAddedElement()
    {
        var baseTree = BuildBaseForm();
        var modifiedTree = TreeMergeEngine.DeepClone(baseTree);
        var tabs = FindFirst(modifiedTree, "tabs")!;
        tabs.Children.Add(
            Tab("{00000000-0000-0000-0000-000000000002}", "newtab",
                Node("columns", Node("column", new Dictionary<string, string> { ["width"] = "100%" }, Node("sections")))));

        var diff = TreeMergeEngine.ComputeDiff(baseTree, modifiedTree);

        var addedTab = FindAll(diff, "tab").FirstOrDefault(t => t.Action == MergeAction.Added);
        Assert.NotNull(addedTab);
        Assert.Equal("{00000000-0000-0000-0000-000000000002}", addedTab!.GetAttribute("id"));
    }

    [Fact]
    public void ComputeDiff_DetectsRemovedElement()
    {
        var baseTree = BuildBaseForm();
        var modifiedTree = TreeMergeEngine.DeepClone(baseTree);
        var sections = FindFirst(modifiedTree, "sections")!;
        var toRemove = sections.Children.First(s => s.GetAttribute("id") == "{00000000-0000-0000-0000-000000000011}");
        sections.Children.Remove(toRemove);

        var diff = TreeMergeEngine.ComputeDiff(baseTree, modifiedTree);

        var removedSection = FindAll(diff, "section").FirstOrDefault(s => s.Action == MergeAction.Removed);
        Assert.NotNull(removedSection);
        Assert.Equal("{00000000-0000-0000-0000-000000000011}", removedSection!.GetAttribute("id"));
    }

    [Fact]
    public void ComputeDiff_IdenticalTrees()
    {
        var baseTree = BuildBaseForm();
        var modifiedTree = TreeMergeEngine.DeepClone(baseTree);

        var diff = TreeMergeEngine.ComputeDiff(baseTree, modifiedTree);

        Assert.Empty(CollectActions(diff));
    }

    [Fact]
    public void Merge_Roundtrip_DiffThenMerge()
    {
        var baseTree = BuildBaseForm();
        var modifiedTree = TreeMergeEngine.DeepClone(baseTree);
        var tabs = FindFirst(modifiedTree, "tabs")!;
        tabs.Children.Add(
            Tab("{00000000-0000-0000-0000-000000000002}", "newtab",
                Node("columns", Node("column", new Dictionary<string, string> { ["width"] = "100%" }, Node("sections")))));

        var diff = TreeMergeEngine.ComputeDiff(baseTree, modifiedTree);
        var result = TreeMergeEngine.Merge(baseTree, diff);

        Assert.Equal(2, FindAll(result, "tab").Count);
    }

    // --- Helpers ---

    private static MergeableNode Node(string name, params MergeableNode[] children)
        => NodeWithAttrs(name, new Dictionary<string, string>(), children);

    private static MergeableNode Node(string name, Dictionary<string, string> attrs, params MergeableNode[] children)
        => NodeWithAttrs(name, attrs, children);

    private static MergeableNode NodeWithAttrs(string name, Dictionary<string, string> attrs, MergeableNode[] children)
    {
        var node = new MergeableNode { Name = name };
        foreach (var kvp in attrs)
            node.Attributes[kvp.Key] = kvp.Value;
        foreach (var child in children)
            node.Children.Add(child);
        return node;
    }

    private static MergeableNode Tab(string id, string tabName, MergeableNode columns, MergeAction? action = null)
    {
        var tab = Node("tab", new Dictionary<string, string> { ["id"] = id, ["name"] = tabName, ["showlabel"] = "true" }, columns);
        tab.Action = action;
        return tab;
    }

    private static MergeableNode Section(string id, string sectionName, MergeableNode rows)
        => Node("section", new Dictionary<string, string> { ["id"] = id, ["name"] = sectionName, ["showlabel"] = "true" }, rows);

    private static MergeableNode SectionShell(string id, string sectionName, MergeAction action)
    {
        var s = new MergeableNode { Name = "section", Action = action };
        s.Attributes["id"] = id;
        s.Attributes["name"] = sectionName;
        return s;
    }

    private static MergeableNode Cell(string id, params MergeableNode[] children)
        => Node("cell", new Dictionary<string, string> { ["id"] = id }, children);

    private static MergeableNode Control(string id, string classId, string datafieldname)
        => Node("control", new Dictionary<string, string> { ["id"] = id, ["classid"] = classId, ["datafieldname"] = datafieldname });

    private static MergeableNode ControlModified(string id, string datafieldname, string disabled)
    {
        var c = Node("control", new Dictionary<string, string> { ["id"] = id, ["datafieldname"] = datafieldname, ["disabled"] = disabled });
        c.Action = MergeAction.Modified;
        return c;
    }

    private static List<MergeableNode> FindAll(MergeableNode root, string name)
    {
        var results = new List<MergeableNode>();
        Collect(root, name, results);
        return results;
    }

    private static void Collect(MergeableNode node, string name, List<MergeableNode> results)
    {
        if (node.Name == name) results.Add(node);
        foreach (var child in node.Children)
            Collect(child, name, results);
    }

    private static MergeableNode? FindFirst(MergeableNode node, string name)
    {
        if (node.Name == name) return node;
        foreach (var child in node.Children)
        {
            var found = FindFirst(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static List<MergeAction> CollectActions(MergeableNode node)
    {
        var actions = new List<MergeAction>();
        CollectActionsRecursive(node, actions);
        return actions;
    }

    private static void CollectActionsRecursive(MergeableNode node, List<MergeAction> actions)
    {
        if (node.Action != null) actions.Add(node.Action.Value);
        foreach (var child in node.Children)
            CollectActionsRecursive(child, actions);
    }
}
