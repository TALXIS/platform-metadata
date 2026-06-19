using System.Xml.Linq;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _validator = new();

    // Entity.xsd requires <Name OriginalName="..." LocalizedName="...">value</Name> (minOccurs=1).
    // EntityInfo and everything else is optional.
    private const string ValidEntityXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <Name OriginalName="test_entity" LocalizedName="Test Entity">test_entity</Name>
          <EntityInfo>
            <entity Name="test_entity">
              <LocalizedNames>
                <LocalizedName description="Test" languagecode="1033" />
              </LocalizedNames>
              <LocalizedCollectionNames>
                <LocalizedCollectionName description="Tests" languagecode="1033" />
              </LocalizedCollectionNames>
              <Descriptions>
                <Description description="" languagecode="1033" />
              </Descriptions>
              <attributes />
              <EntitySetName>test_entities</EntitySetName>
            </entity>
          </EntityInfo>
        </Entity>
        """;

    private const string ValidSolutionXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml version="9.2" SolutionPackageVersion="9.2" languagecode="1033">
          <SolutionManifest>
            <UniqueName>TestSolution</UniqueName>
            <LocalizedNames>
              <LocalizedName description="Test Solution" languagecode="1033" />
            </LocalizedNames>
            <Descriptions></Descriptions>
            <Version>1.0.0.0</Version>
            <Managed>0</Managed>
            <Publisher>
              <UniqueName>test</UniqueName>
              <LocalizedNames>
                <LocalizedName description="Test Publisher" languagecode="1033" />
              </LocalizedNames>
              <Descriptions>
                <Description description="" languagecode="1033" />
              </Descriptions>
              <EMailAddress />
              <SupportingWebsiteUrl />
              <CustomizationPrefix>test</CustomizationPrefix>
              <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
              <Addresses>
                <Address>
                  <AddressNumber>1</AddressNumber>
                  <AddressTypeCode>1</AddressTypeCode>
                </Address>
              </Addresses>
            </Publisher>
            <RootComponents />
            <MissingDependencies />
          </SolutionManifest>
        </ImportExportXml>
        """;

    [Fact]
    public void ValidEntityXml_PassesValidation()
    {
        var doc = XDocument.Parse(ValidEntityXml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        Assert.Empty(results);
    }

    [Fact]
    public void InvalidEntityXml_MissingRequiredElement_FailsValidation()
    {
        // Missing required <Name> element
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <EntityInfo>
                <entity Name="test_entity">
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidationResult_ContainsFilePathAndMessage()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <UnknownElement>bad</UnknownElement>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "src/Entities/Entity.xml");

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal("src/Entities/Entity.xml", first.FilePath);
        Assert.False(string.IsNullOrWhiteSpace(first.Message));
    }

    [Fact]
    public void MultipleErrors_AllCollected()
    {
        // Solution.xsd uses xs:sequence — wrong element order and wrong types
        // produce separate validation events
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml>
              <SolutionManifest>
                <UniqueName>!!!INVALID!!!</UniqueName>
                <LocalizedNames />
                <Descriptions />
                <Version>1.0</Version>
                <Managed>999</Managed>
                <Publisher>
                  <UniqueName>!!!ALSO INVALID!!!</UniqueName>
                  <LocalizedNames />
                  <Descriptions />
                  <EMailAddress />
                  <SupportingWebsiteUrl />
                  <CustomizationPrefix>x</CustomizationPrefix>
                  <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
                  <Addresses />
                </Publisher>
                <RootComponents />
              </SolutionManifest>
            </ImportExportXml>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Solution.xml");

        Assert.True(results.Count > 1, $"Expected multiple errors but got {results.Count}: {string.Join("; ", results.Select(r => r.Message))}");
    }

    [Fact]
    public void ValidSolutionXml_PassesValidation()
    {
        var doc = XDocument.Parse(ValidSolutionXml);
        var results = _validator.ValidateXml(doc, "Solution.xml");

        Assert.Empty(results);
    }

    [Fact]
    public void ResidualCustomizationsXml_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Entities />
              <Roles />
              <Workflows />
              <FieldSecurityProfiles />
              <Templates />
              <EntityMaps />
              <EntityRelationships />
              <OrganizationSettings />
              <optionsets />
              <CustomControls />
              <SolutionPluginAssemblies />
              <EntityDataProviders />
              <Languages>
                <Language>1033</Language>
              </Languages>
            </ImportExportXml>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Customizations.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void SkeletalRelationshipXml_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <EntityRelationship Name="test_relationship" />
            </EntityRelationships>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Relationships.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void SdkMessageProcessingStep_RealFormat_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <SdkMessageProcessingStep Name="Test: Create of entity" SdkMessageProcessingStepId="{8972dfad-506c-45b8-a6fa-83747f170734}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SdkMessageId>9ebdbb1b-ea3e-db11-86a7-000a3a5473e8</SdkMessageId>
              <PluginTypeName>Plugins.Test, TestAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abc123</PluginTypeName>
              <PrimaryEntity>test_entity</PrimaryEntity>
              <PluginTypeId>c7d1ad26-47c5-4774-99b1-aa6203d1a169</PluginTypeId>
              <AsyncAutoDelete>0</AsyncAutoDelete>
              <FilteringAttributes></FilteringAttributes>
              <InvocationSource>0</InvocationSource>
              <Mode>0</Mode>
              <Rank>1</Rank>
              <EventHandlerTypeCode>4602</EventHandlerTypeCode>
              <Stage>10</Stage>
              <IsCustomizable>1</IsCustomizable>
              <IsHidden>0</IsHidden>
              <SupportedDeployment>0</SupportedDeployment>
              <IntroducedVersion>1.0</IntroducedVersion>
              <SdkMessageProcessingStepImages />
            </SdkMessageProcessingStep>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "SdkMessageProcessingStep.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void AppModule_WithAppSettings_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <AppModule>
              <UniqueName>test_app</UniqueName>
              <IntroducedVersion>1.0</IntroducedVersion>
              <WebResourceId>953b9fac-1e5e-e611-80d6-00155ded156f</WebResourceId>
              <statecode>0</statecode>
              <statuscode>1</statuscode>
              <FormFactor>1</FormFactor>
              <ClientType>4</ClientType>
              <AppModuleComponents>
                <AppModuleComponent type="1" schemaName="test_entity" />
              </AppModuleComponents>
              <LocalizedNames>
                <LocalizedName description="Test App" languagecode="1033" />
              </LocalizedNames>
              <appsettings>
                <appsetting settingdefinitionid.uniquename="AppChannel">
                  <iscustomizable>1</iscustomizable>
                  <value>1</value>
                </appsetting>
              </appsettings>
            </AppModule>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "AppModule.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSampleRepo_AllSolutions()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "TestData");
        if (!Directory.Exists(basePath)) return;

        var solutions = new[] { "Solutions.DataModel", "Solutions.UI", "Solutions.Logic", "Solutions.Security" };
        var allErrors = new List<ValidationResult>();

        foreach (var solution in solutions)
        {
            var solutionPath = Path.Combine(basePath, solution);
            if (!Directory.Exists(solutionPath)) continue;

            foreach (var xmlFile in Directory.EnumerateFiles(solutionPath, "*.xml", SearchOption.AllDirectories))
            {
                allErrors.AddRange(_validator.ValidateFile(xmlFile));
            }
        }

        var errors = allErrors.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }



    [Fact]
    public void EnvironmentVariable_WithApiId_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <environmentvariabledefinition schemaname="tp_SomeVar">
              <displayname default="Some Var">
                <label description="Some Var" languagecode="1033" />
              </displayname>
              <introducedversion>1.0</introducedversion>
              <iscustomizable>1</iscustomizable>
              <isrequired>0</isrequired>
              <type>100000000</type>
              <apiid>some-api-id</apiid>
            </environmentvariabledefinition>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "EnvironmentVariable.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void CustomApi_RootElement_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <customapi>
              <name>test_CustomApi</name>
              <displayname>Test Custom API</displayname>
            </customapi>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "CustomApi.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void CustomApiRequestParameter_RootElement_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <customapirequestparameter>
              <uniquename>Target</uniquename>
              <type>10</type>
            </customapirequestparameter>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "CustomApiRequestParameter.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void PluginType_WithDuplicateWorkflowActivityGroupName_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <PluginAssembly FullName="Test.Assembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null">
              <PluginTypes>
                <PluginType AssemblyQualifiedName="Test.Workflow, Test.Assembly">
                  <FriendlyName>Test Workflow</FriendlyName>
                  <WorkflowActivityGroupName>Test Group</WorkflowActivityGroupName>
                  <WorkflowActivityGroupName>Test Group</WorkflowActivityGroupName>
                </PluginType>
              </PluginTypes>
            </PluginAssembly>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "PluginAssembly.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Descriptions_WithDisplayname_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Name OriginalName="test_entity" LocalizedName="Test Entity">test_entity</Name>
              <EntityInfo>
                <entity Name="test_entity">
                  <Descriptions>
                    <Description description="Test" languagecode="1033" />
                    <displayname description="Test Name" languagecode="1033" />
                  </Descriptions>
                  <attributes />
                  <EntitySetName>test_entities</EntitySetName>
                </entity>
              </EntityInfo>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void ResidualCustomizations_WithRibbonDiffXmlAndDashboards_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Entities />
              <Roles />
              <Workflows />
              <RibbonDiffXml />
              <Dashboards />
              <AppModuleSiteMaps />
              <AppModules />
              <Languages>
                <Language>1033</Language>
              </Languages>
            </ImportExportXml>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Customizations.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void FormCell_WithContentTypeAndTypeAttributes_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <forms>
              <systemform>
                <formid>{12345678-1234-1234-1234-123456789012}</formid>
                <form>
                  <tabs>
                    <tab name="general" id="{00000000-0000-0000-0000-000000000001}">
                      <columns>
                        <column width="100%">
                          <sections>
                            <section name="sec" columns="1">
                              <rows>
                                <row>
                                  <cell id="{00000000-0000-0000-0000-000000000002}" contenttype="cardSections" type="something">
                                    <control id="field1" classid="{00000000-0000-0000-0000-000000000003}" datafieldname="field1" />
                                  </cell>
                                </row>
                              </rows>
                            </section>
                          </sections>
                        </column>
                      </columns>
                    </tab>
                  </tabs>
                </form>
              </systemform>
            </forms>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "SystemForms.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void FormTab_WithContentTypeAttribute_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <forms>
              <systemform>
                <formid>{12345678-1234-1234-1234-123456789012}</formid>
                <form>
                  <tabs>
                    <tab name="voucherstab" id="{00000000-0000-0000-0000-000000000001}" contenttype="singleComponent" showlabel="true">
                      <columns>
                        <column width="100%">
                          <sections>
                            <section name="sec" columns="1">
                              <rows />
                            </section>
                          </sections>
                        </column>
                      </columns>
                    </tab>
                  </tabs>
                </form>
              </systemform>
            </forms>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "SystemForms.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Forms_WithTypeAttribute_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <forms type="card">
              <systemform>
                <formid>{12345678-1234-1234-1234-123456789012}</formid>
                <form>
                  <tabs>
                    <tab name="general" id="{00000000-0000-0000-0000-000000000001}">
                      <columns>
                        <column width="100%">
                          <sections>
                            <section name="sec" columns="1">
                              <rows />
                            </section>
                          </sections>
                        </column>
                      </columns>
                    </tab>
                  </tabs>
                </form>
              </systemform>
            </forms>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "SystemForms.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Connector_RootElement_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Connector xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <connectorid>12896af7-da5f-4734-b86a-48d95997ca31</connectorid>
              <description>ComposeLine</description>
              <displayname>ntg_composeoutputlinetxt</displayname>
              <name>ntg_ntg-5fcomposeoutputlinetxt</name>
              <connectortype>1</connectortype>
            </Connector>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Connector.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void EnvironmentVariable_WithParameterKey_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <environmentvariabledefinition schemaname="tp_SomeVar">
              <displayname default="Some Var">
                <label description="Some Var" languagecode="1033" />
              </displayname>
              <introducedversion>1.0</introducedversion>
              <iscustomizable>1</iscustomizable>
              <isrequired>0</isrequired>
              <type>100000000</type>
              <parameterkey>dataset</parameterkey>
            </environmentvariabledefinition>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "EnvironmentVariable.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Descriptions_WithMixedOrder_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Name OriginalName="test_entity" LocalizedName="Test Entity">test_entity</Name>
              <EntityInfo>
                <entity Name="test_entity">
                  <Descriptions>
                    <displayname description="Test Name" languagecode="1033" />
                    <Description description="Test" languagecode="1033" />
                    <displayname description="Another Name" languagecode="1029" />
                  </Descriptions>
                  <attributes />
                  <EntitySetName>test_entities</EntitySetName>
                </entity>
              </EntityInfo>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void AppModule_WithUniquenameAndSettingDefinitionId_PassesValidation()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <AppModule>
              <UniqueName>test_app</UniqueName>
              <IntroducedVersion>1.0</IntroducedVersion>
              <WebResourceId>953b9fac-1e5e-e611-80d6-00155ded156f</WebResourceId>
              <AppModuleComponents>
                <AppModuleComponent type="1" schemaName="test_entity" />
              </AppModuleComponents>
              <LocalizedNames>
                <LocalizedName description="Test App" languagecode="1033" />
              </LocalizedNames>
              <appsettings>
                <appsetting uniquename="test_AllowNotifications">
                  <iscustomizable>1</iscustomizable>
                  <settingdefinitionid>
                    <uniquename>AllowNotificationsEarlyAccess</uniquename>
                  </settingdefinitionid>
                  <value>true</value>
                </appsetting>
              </appsettings>
            </AppModule>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "AppModule.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void FieldSecurityProfile_WithCanReadUnmasked_PassesValidation()
    {
        // CanReadUnmasked is a valid FieldPermission element (column masking feature).
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <FieldSecurityProfiles xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <FieldSecurityProfile name="Test" fieldsecurityprofileid="{67560be0-fd60-ee11-8df0-000d3adf7d8e}">
                <FieldPermissions>
                  <FieldPermission>
                    <EntityName>pba_projekt</EntityName>
                    <AttributeName>pba_field</AttributeName>
                    <CanRead>4</CanRead>
                    <CanUpdate>0</CanUpdate>
                    <CanCreate>0</CanCreate>
                    <CanReadUnmasked>0</CanReadUnmasked>
                  </FieldPermission>
                </FieldPermissions>
              </FieldSecurityProfile>
            </FieldSecurityProfiles>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "FieldSecurityProfiles.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Customizations_WithImageConfigs_PassesValidation()
    {
        // EntityImageConfigs / AttributeImageConfigs appear when an entity has an image/file column.
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Entities />
              <EntityImageConfigs>
                <EntityImageConfig>
                  <parententitylogicalname>pba_nahlasenechyby</parententitylogicalname>
                  <primaryimageattribute>pba_screenshot</primaryimageattribute>
                </EntityImageConfig>
              </EntityImageConfigs>
              <AttributeImageConfigs>
                <AttributeImageConfig>
                  <attributelogicalname>pba_screenshot</attributelogicalname>
                  <parententitylogicalname>pba_nahlasenechyby</parententitylogicalname>
                  <canstorefullimage>1</canstorefullimage>
                </AttributeImageConfig>
              </AttributeImageConfigs>
              <Languages>
                <Language>1033</Language>
              </Languages>
            </ImportExportXml>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Customizations.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
        // The image-config elements must be fully modeled — no "could not find
        // schema information" warnings for them or their children.
        Assert.DoesNotContain(results, r =>
            r.Severity == ValidationSeverity.Warning &&
            (r.Message.Contains("ImageConfig") || r.Message.Contains("primaryimageattribute")
             || r.Message.Contains("attributelogicalname") || r.Message.Contains("canstorefullimage")
             || r.Message.Contains("parententitylogicalname")));
    }

    [Fact]
    public void OptionSet_StateAndStatus_WithIsHidden_PassesValidation()
    {
        // IsHidden is valid on statecode <state> and statuscode <status> values.
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <optionset Name="pba_test_statecode">
              <OptionSetType>state</OptionSetType>
              <states>
                <state value="0" defaultstatus="1" invariantname="Active" IsHidden="0">
                  <labels>
                    <label description="Active" languagecode="1033" />
                  </labels>
                </state>
              </states>
              <statuses>
                <status value="1" state="0" IsHidden="0">
                  <labels>
                    <label description="Active" languagecode="1033" />
                  </labels>
                </status>
              </statuses>
            </optionset>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "OptionSet.xml");

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }
}
