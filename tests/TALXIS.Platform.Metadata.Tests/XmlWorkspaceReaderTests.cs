using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlWorkspaceReaderTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void Load_ParsesSolution()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        var solution = Assert.Single(workspace.Solutions);

        Assert.Equal("TestSolution", solution.UniqueName);
        Assert.Equal("1.0.0.0", solution.Version);
        Assert.NotNull(solution.Publisher);
        Assert.Equal("TestPub", solution.Publisher.UniqueName);
        Assert.Equal("tp", solution.Publisher.Prefix);
        Assert.Equal(10000, solution.Publisher.OptionValuePrefix);
        Assert.Single(solution.RootComponents);
    }

    [Fact]
    public void Load_ParsesEntities()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.Single(workspace.Entities);
        var entity = workspace.FindEntity("test_entity");
        Assert.NotNull(entity);
        Assert.Equal("test_entity", entity.LogicalName);
        Assert.Equal("Test Entity", entity.DisplayName.Default);
        Assert.Equal("Test Entities", entity.PluralName.Default);
        Assert.Equal("test_entities", entity.EntitySetName);
        Assert.True(entity.Attributes.Count >= 2);
    }

    [Fact]
    public void Load_ParsesAttributes()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var entity = workspace.FindEntity("test_entity")!;

        var nameAttr = entity.FindAttribute("tp_name");
        Assert.NotNull(nameAttr);
        Assert.IsType<StringAttributeMetadata>(nameAttr);
        Assert.Equal("Name", nameAttr.DisplayName.Default);

        var countAttr = entity.FindAttribute("tp_count");
        Assert.NotNull(countAttr);
        Assert.IsType<IntegerAttributeMetadata>(countAttr);
    }

    [Fact]
    public void Load_PreservesDistinctDataverseRequiredLevels()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-requiredlevel-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "required_entity"));

            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>T</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>t</UniqueName>
                      <CustomizationPrefix>t</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            File.WriteAllText(Path.Combine(dir, "Entities", "required_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="required_entity">
                      <EntitySetName>required_entities</EntitySetName>
                      <LocalizedNames><LocalizedName description="Required Entity" languagecode="1033" /></LocalizedNames>
                      <LocalizedCollectionNames><LocalizedCollectionName description="Required Entities" languagecode="1033" /></LocalizedCollectionNames>
                      <attributes>
                        <attribute PhysicalName="app_required">
                          <Type>nvarchar</Type>
                          <LogicalName>app_required</LogicalName>
                          <RequiredLevel>applicationrequired</RequiredLevel>
                        </attribute>
                        <attribute PhysicalName="sys_required">
                          <Type>nvarchar</Type>
                          <LogicalName>sys_required</LogicalName>
                          <RequiredLevel>systemrequired</RequiredLevel>
                        </attribute>
                        <attribute PhysicalName="required">
                          <Type>nvarchar</Type>
                          <LogicalName>required</LogicalName>
                          <RequiredLevel>required</RequiredLevel>
                        </attribute>
                      </attributes>
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var entity = workspace.FindEntity("required_entity")!;

            Assert.Equal(RequiredLevel.ApplicationRequired, entity.FindAttribute("app_required")!.RequiredLevel);
            Assert.Equal(RequiredLevel.SystemRequired, entity.FindAttribute("sys_required")!.RequiredLevel);
            Assert.Equal(RequiredLevel.Required, entity.FindAttribute("required")!.RequiredLevel);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_PreservesRawManagedValue()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-managed-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>T</UniqueName>
                    <Version>1.0</Version>
                    <Managed>2</Managed>
                    <Publisher>
                      <UniqueName>t</UniqueName>
                      <CustomizationPrefix>t</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var solution = Assert.Single(workspace.Solutions);

            Assert.Equal("2", solution.ManagedValue);
            Assert.True(solution.IsManaged);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_ParsesGlobalOptionSets()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.Single(workspace.GlobalOptionSets);
        var os = workspace.GlobalOptionSets[0];
        Assert.Equal("tp_teststatus", os.Name);
        Assert.True(os.IsGlobal);
        Assert.Equal(2, os.Options.Count);
        Assert.Equal("Active", os.Options[0].Label.Default);
        Assert.Equal(100000000, os.Options[0].Value);
    }

    [Fact]
    public void Load_SetsSourceLocation()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        var solution = Assert.Single(workspace.Solutions);

        Assert.NotNull(solution.Source);
        Assert.Contains("Solution.xml", solution.Source.FilePath);

        var entity = workspace.FindEntity("test_entity");
        Assert.NotNull(entity?.Source);
        Assert.Contains("Entity.xml", entity.Source.FilePath);

        Assert.NotNull(workspace.GlobalOptionSets[0].Source);
    }

    [Fact]
    public void Load_SourceLocations_UseElementLineInfo()
    {
        var workspace = new XmlWorkspaceReader().Load(SamplePath);
        var solution = Assert.Single(workspace.Solutions);

        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Other", "Solution.xml"), 3, 4), solution.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Other", "Solution.xml"), 11, 6), solution.Publisher!.Source);

        var entity = workspace.FindEntity("test_entity")!;
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 4, 6), entity.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 15, 10), entity.FindAttribute("tp_name")!.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 40, 10), entity.FindAttribute("tp_count")!.Source);
    }

    [Fact]
    public void Load_MalformedRibbonDiff_AddsLoadError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-ribbon-error-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "account", "RibbonDiffXml"));
            var ribbonPath = Path.Combine(dir, "Entities", "account", "RibbonDiffXml", "RibbonDiff.xml");
            File.WriteAllText(ribbonPath, "<RibbonDiffXml><CustomActions>");

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Empty(workspace.Ribbons);
            var error = Assert.Single(workspace.LoadErrors);
            Assert.Equal(ribbonPath, error.FilePath);
            Assert.Contains("Malformed XML", error.Message);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_EntityCountMatchesFolders()
    {
        var entityFolderCount = Directory.GetDirectories(Path.Combine(SamplePath, "Entities")).Length;
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        Assert.Equal(entityFolderCount, workspace.Entities.Count);
    }

    [Fact]
    public void Load_RootComponentCountMatchesSolutionXml()
    {
        var solutionXml = System.Xml.Linq.XDocument.Load(Path.Combine(SamplePath, "Other", "Solution.xml"));
        var expectedCount = solutionXml.Root!
            .Element("SolutionManifest")!
            .Element("RootComponents")!
            .Elements("RootComponent")
            .Count();

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        Assert.Equal(expectedCount, Assert.Single(workspace.Solutions).RootComponents.Count);
    }

    [Fact]
    public void Load_EntityWithNoAttributes_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "bare_entity"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");
            File.WriteAllText(Path.Combine(dir, "Entities", "bare_entity", "Entity.xml"),
                "<?xml version=\"1.0\"?><Entity><EntityInfo><entity Name=\"bare_entity\"><EntitySetName>bare_entities</EntitySetName><LocalizedNames><LocalizedName description=\"Bare\" languagecode=\"1033\"/></LocalizedNames><LocalizedCollectionNames><LocalizedCollectionName description=\"Bares\" languagecode=\"1033\"/></LocalizedCollectionNames><attributes/></entity></EntityInfo></Entity>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.FindEntity("bare_entity")!.Attributes);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_NoOptionSetsFolder_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.GlobalOptionSets);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_NoRelationshipsXml_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.Relationships);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_PerEntityRelationships_MergeWithMonolithicAndAttachToEntities()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-relationships-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other", "Relationships"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "account"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "contact"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");
            File.WriteAllText(Path.Combine(dir, "Entities", "account", "Entity.xml"),
                "<?xml version=\"1.0\"?><Entity><EntityInfo><entity Name=\"account\"><LocalizedNames><LocalizedName description=\"Account\" languagecode=\"1033\" /></LocalizedNames><attributes /></entity></EntityInfo></Entity>");
            File.WriteAllText(Path.Combine(dir, "Entities", "contact", "Entity.xml"),
                "<?xml version=\"1.0\"?><Entity><EntityInfo><entity Name=\"contact\"><LocalizedNames><LocalizedName description=\"Contact\" languagecode=\"1033\" /></LocalizedNames><attributes /></entity></EntityInfo></Entity>");
            File.WriteAllText(Path.Combine(dir, "Other", "Relationships.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships>
                  <EntityRelationship Name="account_contact_parentcustomerid" />
                </EntityRelationships>
                """);
            File.WriteAllText(Path.Combine(dir, "Other", "Relationships", "account.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships>
                  <EntityRelationship Name="account_contact_parentcustomerid">
                    <EntityRelationshipType>OneToMany</EntityRelationshipType>
                    <IsCustomizable>1</IsCustomizable>
                    <IntroducedVersion>1.0.0.0</IntroducedVersion>
                    <IsHierarchical>0</IsHierarchical>
                    <ReferencingEntityName>contact</ReferencingEntityName>
                    <ReferencedEntityName>account</ReferencedEntityName>
                    <CascadeAssign>NoCascade</CascadeAssign>
                    <CascadeDelete>Cascade</CascadeDelete>
                    <CascadeArchive>RemoveLink</CascadeArchive>
                    <CascadeReparent>NoCascade</CascadeReparent>
                    <CascadeShare>NoCascade</CascadeShare>
                    <CascadeUnshare>NoCascade</CascadeUnshare>
                    <CascadeRollupView>NoCascade</CascadeRollupView>
                    <IsValidForAdvancedFind>1</IsValidForAdvancedFind>
                    <ReferencingAttributeName>parentcustomerid</ReferencingAttributeName>
                    <EntityRelationshipRoles>
                      <EntityRelationshipRole>
                        <NavPaneDisplayOption>UseCollectionName</NavPaneDisplayOption>
                        <NavPaneArea>Details</NavPaneArea>
                        <NavPaneOrder>10000</NavPaneOrder>
                        <NavigationPropertyName>parentcustomerid_account</NavigationPropertyName>
                        <RelationshipRoleType>1</RelationshipRoleType>
                      </EntityRelationshipRole>
                    </EntityRelationshipRoles>
                  </EntityRelationship>
                </EntityRelationships>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            var relationship = Assert.IsType<OneToManyRelationshipMetadata>(Assert.Single(workspace.Relationships));
            Assert.Equal("account", relationship.ReferencedEntity);
            Assert.Equal("contact", relationship.ReferencingEntity);
            Assert.Equal("parentcustomerid", relationship.ReferencingAttribute);
            Assert.Equal(CascadeType.Cascade, relationship.CascadeDelete);
            Assert.True(relationship.IsCustomizable);
            Assert.Equal("1.0.0.0", relationship.IntroducedVersion);
            Assert.True(relationship.IsValidForAdvancedFind);
            Assert.Single(relationship.Roles);
            Assert.Equal("parentcustomerid_account", relationship.Roles[0].NavigationPropertyName);
            Assert.Single(workspace.FindEntity("account")!.Relationships);
            Assert.Single(workspace.FindEntity("contact")!.Relationships);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_Forms_ReadsMergeableBodyWithSolutionActionAndOrdinalValue()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-form-body-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "account", "FormXml", "main"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");
            File.WriteAllText(Path.Combine(dir, "Entities", "account", "FormXml", "main", "{11111111-1111-1111-1111-111111111111}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <forms>
                  <systemform>
                    <formid>{11111111-1111-1111-1111-111111111111}</formid>
                    <form>
                      <tabs>
                        <tab id="base-tab" ordinalvalue="20" />
                        <tab id="added-tab" solutionaction="Added" ordinalvalue="10" />
                      </tabs>
                    </form>
                  </systemform>
                </forms>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            var form = Assert.Single(workspace.Forms);
            Assert.NotNull(form.Body);
            var tabs = FindAll(form.Body!, "tab");
            Assert.Equal(2, tabs.Count);
            Assert.Equal("20", tabs[0].GetAttribute("ordinalvalue"));
            Assert.Equal(MergeAction.Added, tabs[1].Action);
            Assert.Null(tabs[1].GetAttribute("solutionaction"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_InvalidPath_ThrowsWithPath()
    {
        var ex = Assert.Throws<DirectoryNotFoundException>(() => new XmlWorkspaceReader().Load("/nonexistent/path"));
        Assert.Contains("/nonexistent/path", ex.Message);
    }

    [Fact]
    public void LoadMany_LoadsManagedAndUnmanagedState_WithActiveOverManaged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"reader-loadmany-{Guid.NewGuid():N}");
        var unmanagedPath = Path.Combine(root, "unmanaged");
        var managedPath = Path.Combine(root, "managed");

        try
        {
            WriteMinimalEntityWorkspace(unmanagedPath, "UnmanagedUi", managed: false, "Active Account");
            WriteMinimalEntityWorkspace(managedPath, "ManagedBase", managed: true, "Managed Account");

            var workspace = new XmlWorkspaceReader().LoadMany(new[]
            {
                new SolutionWorkspaceSource(unmanagedPath, 0),
                new SolutionWorkspaceSource(managedPath, 10)
            });

            Assert.Equal(2, workspace.Solutions.Count);
            Assert.Equal(2, workspace.SolutionComponents.Count);
            Assert.Equal(2, workspace.ComponentSources.Count);

            var stack = workspace.Layers.FindStack(ComponentType.Entity, "account");
            Assert.NotNull(stack);
            Assert.Equal(2, stack!.Layers.Count);
            Assert.Equal("ManagedBase", stack.BaseLayer!.SolutionUniqueName);
            Assert.Equal(SolutionLayerManager.ActiveSolutionName, stack.TopLayer!.SolutionUniqueName);
            Assert.Equal("UnmanagedUi", stack.TopLayer.SourceSolutionUniqueName);

            var resolved = Assert.IsType<EntityMetadata>(workspace.Layers.Resolve(stack));
            Assert.Equal("Active Account", resolved.DisplayName.Default);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LoadMany_SourceWithoutSingleSolution_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), $"reader-loadmany-nosolution-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(root, "source");

        try
        {
            Directory.CreateDirectory(sourcePath);

            var ex = Assert.Throws<InvalidOperationException>(() => new XmlWorkspaceReader().LoadMany(new[]
            {
                new SolutionWorkspaceSource(sourcePath, 0)
            }));

            Assert.Contains("exactly one solution", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
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

    private static void WriteMinimalEntityWorkspace(string path, string solutionName, bool managed, string entityDisplayName)
    {
        Directory.CreateDirectory(Path.Combine(path, "Other"));
        Directory.CreateDirectory(Path.Combine(path, "Entities", "account"));
        File.WriteAllText(Path.Combine(path, "Other", "Solution.xml"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml>
              <SolutionManifest>
                <UniqueName>{{solutionName}}</UniqueName>
                <Version>1.0</Version>
                <Managed>{{(managed ? "1" : "0")}}</Managed>
                <Publisher>
                  <UniqueName>test</UniqueName>
                  <CustomizationPrefix>test</CustomizationPrefix>
                </Publisher>
                <RootComponents>
                  <RootComponent type="1" schemaName="account" behavior="2" />
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);
        File.WriteAllText(Path.Combine(path, "Entities", "account", "Entity.xml"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity>
              <Name LocalizedName="{{entityDisplayName}}" OriginalName="{{entityDisplayName}}">account</Name>
              <EntityInfo>
                <entity Name="account">
                  <EntitySetName>accounts</EntitySetName>
                  <LocalizedNames><LocalizedName description="{{entityDisplayName}}" languagecode="1033" /></LocalizedNames>
                  <LocalizedCollectionNames><LocalizedCollectionName description="Accounts" languagecode="1033" /></LocalizedCollectionNames>
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }
}
