using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class ExpandedReaderTests
{
    private const string MinimalSolutionXml =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml version="9.1">
          <SolutionManifest>
            <UniqueName>TestSolution</UniqueName>
            <Version>1.0</Version>
            <Managed>0</Managed>
            <Publisher>
              <UniqueName>test</UniqueName>
              <CustomizationPrefix>test</CustomizationPrefix>
            </Publisher>
            <RootComponents />
          </SolutionManifest>
        </ImportExportXml>
        """;

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), $"expanded-test-{Guid.NewGuid():N}");

    private static void WriteSolution(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Other"));
        File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"), MinimalSolutionXml);
    }

    [Fact]
    public void LoadForms_ParsesFormMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var formDir = Path.Combine(dir, "Entities", "test_entity", "FormXml", "main");
            Directory.CreateDirectory(formDir);
            File.WriteAllText(Path.Combine(formDir, "{a1b2c3d4-0000-0000-0000-000000000001}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <forms>
                  <systemform>
                    <formid>{a1b2c3d4-0000-0000-0000-000000000001}</formid>
                    <IntroducedVersion>1.0.0.0</IntroducedVersion>
                    <FormPresentation>1</FormPresentation>
                    <FormActivationState>1</FormActivationState>
                    <LocalizedNames>
                      <LocalizedName description="Main Form" languagecode="1033" />
                    </LocalizedNames>
                    <Descriptions>
                      <Description description="Primary form" languagecode="1033" />
                    </Descriptions>
                  </systemform>
                </forms>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.Forms);
            var form = workspace.Forms[0];
            Assert.Equal("{a1b2c3d4-0000-0000-0000-000000000001}", form.FormId);
            Assert.Equal("main", form.FormType);
            Assert.Equal("test_entity", form.EntityLogicalName);
            Assert.Equal("Main Form", form.DisplayName.Default);
            Assert.Equal(1, form.FormPresentation);
            Assert.Equal(1, form.FormActivationState);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadViews_ParsesSavedQueryMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var viewsDir = Path.Combine(dir, "Entities", "test_entity", "SavedQueries");
            Directory.CreateDirectory(viewsDir);
            File.WriteAllText(Path.Combine(viewsDir, "{b1b2c3d4-0000-0000-0000-000000000002}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <savedqueries>
                  <savedquery>
                    <savedqueryid>{b1b2c3d4-0000-0000-0000-000000000002}</savedqueryid>
                    <querytype>0</querytype>
                    <isdefault>1</isdefault>
                    <IntroducedVersion>1.0.0.0</IntroducedVersion>
                    <fetchxml><![CDATA[<fetch><entity name="test_entity"><attribute name="test_name" /></entity></fetch>]]></fetchxml>
                    <layoutxml><![CDATA[<grid><row><cell name="test_name" width="200" /></row></grid>]]></layoutxml>
                    <LocalizedNames>
                      <LocalizedName description="Active Records" languagecode="1033" />
                    </LocalizedNames>
                    <Descriptions>
                      <Description description="View of active records" languagecode="1033" />
                    </Descriptions>
                  </savedquery>
                </savedqueries>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.Views);
            var view = workspace.Views[0];
            Assert.Equal("{b1b2c3d4-0000-0000-0000-000000000002}", view.SavedQueryId);
            Assert.Equal(0, view.QueryType);
            Assert.True(view.IsDefault);
            Assert.Equal("test_entity", view.EntityLogicalName);
            Assert.Equal("Active Records", view.DisplayName.Default);
            Assert.Contains("test_entity", view.FetchXml);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadWebResources_ParsesWebResourceMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var wrDir = Path.Combine(dir, "WebResources");
            Directory.CreateDirectory(wrDir);
            File.WriteAllText(Path.Combine(wrDir, "test_resource.data.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <WebResource>
                  <WebResourceId>{c1b2c3d4-0000-0000-0000-000000000003}</WebResourceId>
                  <Name>test_resource.js</Name>
                  <DisplayName>Test Resource</DisplayName>
                  <WebResourceType>3</WebResourceType>
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <IsCustomizable>1</IsCustomizable>
                  <CanBeDeleted>1</CanBeDeleted>
                  <IsHidden>0</IsHidden>
                  <IsEnabledForMobileClient>0</IsEnabledForMobileClient>
                </WebResource>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.WebResources);
            var wr = workspace.WebResources[0];
            Assert.Equal("{c1b2c3d4-0000-0000-0000-000000000003}", wr.WebResourceId);
            Assert.Equal("test_resource.js", wr.Name);
            Assert.Equal("Test Resource", wr.DisplayName.Default);
            Assert.Equal(3, wr.WebResourceType);
            Assert.True(wr.IsCustomizable);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadWorkflows_ParsesWorkflowMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var wfDir = Path.Combine(dir, "Workflows");
            Directory.CreateDirectory(wfDir);
            File.WriteAllText(Path.Combine(wfDir, "test-d1b2c3d4-0000-0000-0000-000000000004.xaml.data.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Workflow WorkflowId="{d1b2c3d4-0000-0000-0000-000000000004}">
                  <Category>0</Category>
                  <Type>1</Type>
                  <PrimaryEntity>test_entity</PrimaryEntity>
                  <Mode>0</Mode>
                  <Scope>1</Scope>
                  <UniqueName>test_workflow</UniqueName>
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <IsCustomizable>1</IsCustomizable>
                  <TriggerOnCreate>1</TriggerOnCreate>
                  <TriggerOnDelete>0</TriggerOnDelete>
                  <OnDemand>0</OnDemand>
                  <LocalizedNames>
                    <LocalizedName description="Test Workflow" languagecode="1033" />
                  </LocalizedNames>
                </Workflow>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.Workflows);
            var wf = workspace.Workflows[0];
            Assert.Equal("{d1b2c3d4-0000-0000-0000-000000000004}", wf.WorkflowId);
            Assert.Equal(0, wf.Category);
            Assert.Equal("test_entity", wf.PrimaryEntity);
            Assert.Equal("Test Workflow", wf.DisplayName.Default);
            Assert.True(wf.TriggerOnCreate);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadPluginAssemblies_ParsesWithPluginTypes()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var pluginsDir = Path.Combine(dir, "PluginAssemblies");
            Directory.CreateDirectory(pluginsDir);
            File.WriteAllText(Path.Combine(pluginsDir, "Test.dll.data.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <PluginAssembly PluginAssemblyId="{e1b2c3d4-0000-0000-0000-000000000005}" FullName="Test, Version=1.0.0.0">
                  <IsolationMode>2</IsolationMode>
                  <SourceType>0</SourceType>
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <PluginTypes>
                    <PluginType PluginTypeId="{f1b2c3d4-0000-0000-0000-000000000006}" Name="Test.Plugins.OnCreate">
                      <FriendlyName>On Create</FriendlyName>
                      <TypeName>Test.Plugins.OnCreate</TypeName>
                    </PluginType>
                  </PluginTypes>
                </PluginAssembly>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.PluginAssemblies);
            var asm = workspace.PluginAssemblies[0];
            Assert.Equal("{e1b2c3d4-0000-0000-0000-000000000005}", asm.PluginAssemblyId);
            Assert.Equal(2, asm.IsolationMode);
            Assert.Single(asm.PluginTypes);
            Assert.Equal("{f1b2c3d4-0000-0000-0000-000000000006}", asm.PluginTypes[0].PluginTypeId);
            Assert.Equal("Test.Plugins.OnCreate", asm.PluginTypes[0].TypeName);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadSdkMessageProcessingSteps_ParsesStepMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var stepsDir = Path.Combine(dir, "SdkMessageProcessingSteps");
            Directory.CreateDirectory(stepsDir);
            File.WriteAllText(Path.Combine(stepsDir, "{01020304-0000-0000-0000-000000000007}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <SdkMessageProcessingStep SdkMessageProcessingStepId="{01020304-0000-0000-0000-000000000007}" Name="Test.OnCreate: Create of test_entity">
                  <SdkMessageId>{11111111-1111-1111-1111-111111111111}</SdkMessageId>
                  <PluginTypeName>Test.Plugins.OnCreate</PluginTypeName>
                  <PluginTypeId>{f1b2c3d4-0000-0000-0000-000000000006}</PluginTypeId>
                  <Stage>20</Stage>
                  <Mode>0</Mode>
                  <Rank>1</Rank>
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <IsCustomizable>1</IsCustomizable>
                  <IsHidden>0</IsHidden>
                </SdkMessageProcessingStep>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.SdkMessageProcessingSteps);
            var step = workspace.SdkMessageProcessingSteps[0];
            Assert.Equal("{01020304-0000-0000-0000-000000000007}", step.SdkMessageProcessingStepId);
            Assert.Equal(20, step.Stage);
            Assert.Equal(0, step.Mode);
            Assert.Equal("Test.Plugins.OnCreate", step.PluginTypeName);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadSecurityRoles_ParsesRoleWithPrivileges()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var rolesDir = Path.Combine(dir, "Roles");
            Directory.CreateDirectory(rolesDir);
            File.WriteAllText(Path.Combine(rolesDir, "TestRole.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Role id="{a0a0a0a0-0000-0000-0000-000000000008}" name="Test Role" isinherited="0">
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <RolePrivileges>
                    <RolePrivilege name="prvCreatetest_entity" level="Global" />
                    <RolePrivilege name="prvReadtest_entity" level="Organization" />
                  </RolePrivileges>
                </Role>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.SecurityRoles);
            var role = workspace.SecurityRoles[0];
            Assert.Equal("{a0a0a0a0-0000-0000-0000-000000000008}", role.RoleId);
            Assert.Equal("Test Role", role.Name);
            Assert.False(role.IsInherited);
            Assert.Equal(2, role.Privileges.Count);
            Assert.Equal("prvCreatetest_entity", role.Privileges[0].Name);
            Assert.Equal("Global", role.Privileges[0].Level);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadAppModules_ParsesAppModuleMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var appDir = Path.Combine(dir, "AppModules", "test_app");
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "AppModule.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModule>
                  <UniqueName>test_app</UniqueName>
                  <IntroducedVersion>1.0.0.0</IntroducedVersion>
                  <FormFactor>3</FormFactor>
                  <ClientType>4</ClientType>
                  <LocalizedNames>
                    <LocalizedName description="Test App" languagecode="1033" />
                  </LocalizedNames>
                  <AppModuleComponents>
                    <AppModuleComponent type="1" schemaName="test_entity" />
                    <AppModuleComponent type="26" id="{a1b2c3d4-0000-0000-0000-000000000001}" />
                  </AppModuleComponents>
                  <AppModuleRoleMaps>
                    <Role id="{a0a0a0a0-0000-0000-0000-000000000008}" />
                    <Role id="{b0b0b0b0-0000-0000-0000-000000000009}" />
                  </AppModuleRoleMaps>
                </AppModule>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.AppModules);
            var app = workspace.AppModules[0];
            Assert.Equal("test_app", app.UniqueName);
            Assert.Equal("Test App", app.DisplayName.Default);
            Assert.Equal(2, app.Components.Count);
            Assert.Equal(2, app.RoleIds.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadSiteMaps_ParsesSiteMapMetadata()
    {
        var dir = CreateTempDir();
        try
        {
            WriteSolution(dir);
            var smDir = Path.Combine(dir, "AppModuleSiteMaps", "test_app");
            Directory.CreateDirectory(smDir);
            File.WriteAllText(Path.Combine(smDir, "AppModuleSiteMap.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <AppModuleSiteMap>
                  <SiteMapUniqueName>test_app_sitemap</SiteMapUniqueName>
                  <ShowHome>True</ShowHome>
                  <ShowPinned>False</ShowPinned>
                  <ShowRecents>True</ShowRecents>
                  <EnableCollapsibleGroups>True</EnableCollapsibleGroups>
                  <LocalizedNames>
                    <LocalizedName description="Test SiteMap" languagecode="1033" />
                  </LocalizedNames>
                  <SiteMap IntroducedVersion="1.0.0.0">
                    <Area Id="Main">
                      <Group Id="Group1">
                        <SubArea Id="test_entity" Entity="test_entity" />
                      </Group>
                    </Area>
                  </SiteMap>
                </AppModuleSiteMap>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Single(workspace.SiteMaps);
            var sm = workspace.SiteMaps[0];
            Assert.Equal("test_app_sitemap", sm.UniqueName);
            Assert.True(sm.ShowHome);
            Assert.False(sm.ShowPinned);
            Assert.True(sm.ShowRecents);
            Assert.Equal("Test SiteMap", sm.DisplayName.Default);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
