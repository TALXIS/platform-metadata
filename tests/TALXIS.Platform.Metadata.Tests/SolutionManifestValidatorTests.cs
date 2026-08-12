using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionManifestValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"manifest-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void StepReferencingExternalAssembly_ReportsWarning()
    {
        // Assemblies may legitimately ship from another solution (e.g. a base product) while
        // the step is registered in this repository — that is a heads-up, not a build breaker.
        WriteSolution("<RootComponent type=\"1\" schemaName=\"test_entity\" behavior=\"0\" />");
        WriteEntity("test_entity");
        WriteStep("Base.Plugins.OnCreate, Base.Product.Plugins");

        var finding = Assert.Single(Validate(), r => r.Code == ValidationDiagnostics.StepAssemblyNotInSolution);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("Base.Product.Plugins", finding.Message);
    }

    [Fact]
    public void DeclaredWorkflowWithoutFile_ReportsError()
    {
        var workflowId = Guid.NewGuid();
        WriteSolution($"<RootComponent type=\"29\" id=\"{{{workflowId}}}\" behavior=\"0\" />");

        var finding = Assert.Single(Validate(), r => r.Code == ValidationDiagnostics.RootComponentFileAbsent);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains(workflowId.ToString(), finding.Message);
    }

    [Fact]
    public void DeclaredEntityWithFile_NoFinding()
    {
        WriteSolution("<RootComponent type=\"1\" schemaName=\"test_entity\" behavior=\"0\" />");
        WriteEntity("test_entity");

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.RootComponentFileAbsent);
    }

    [Fact]
    public void DeclaredEntityCasingDiffers_NoFinding()
    {
        WriteSolution("<RootComponent type=\"1\" schemaName=\"Test_Entity\" behavior=\"0\" />");
        WriteEntity("test_entity");

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.RootComponentFileAbsent);
    }

    [Fact]
    public void EntityOnDiskNotDeclared_ReportsWarning()
    {
        WriteSolution(rootComponents: "");
        WriteEntity("test_entity");

        var finding = Assert.Single(Validate(), r => r.Code == ValidationDiagnostics.ComponentNotDeclaredAsRootComponent);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("test_entity", finding.Message);
    }

    [Fact]
    public void FormNotDeclaredAsRootComponent_NoFinding()
    {
        WriteSolution("<RootComponent type=\"1\" schemaName=\"test_entity\" behavior=\"0\" />");
        WriteEntity("test_entity");
        WriteForm("test_entity", Guid.NewGuid());

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.ComponentNotDeclaredAsRootComponent);
    }

    [Fact]
    public void AppModuleDeclaredById_SkippedRatherThanFlagged()
    {
        WriteSolution($"<RootComponent type=\"80\" id=\"{{{Guid.NewGuid()}}}\" behavior=\"0\" />");

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.RootComponentFileAbsent);
    }

    [Fact]
    public void DuplicateCanvasAppSchemaName_ReportsError()
    {
        // Mirrors what AddRootComponentToSolution produces for a Code App reference: a
        // CanvasApp (type 300) root component identified by SchemaName rather than Id. Two Code
        // Apps sharing an AppName collapse to the same SchemaName here.
        WriteSolution("""
            <RootComponent type="300" schemaName="tp_warehousepicking" behavior="0" />
            <RootComponent type="300" schemaName="tp_warehousepicking" behavior="0" />
            """);

        var finding = Assert.Single(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("tp_warehousepicking", finding.Message);
        Assert.Contains("CanvasApp", finding.Message);
    }

    [Fact]
    public void DuplicateSchemaNameCasingDiffers_ReportsError()
    {
        WriteSolution("""
            <RootComponent type="300" schemaName="tp_warehousepicking" behavior="0" />
            <RootComponent type="300" schemaName="TP_WarehousePicking" behavior="0" />
            """);

        Assert.Contains(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
    }

    [Fact]
    public void SameSchemaNameDifferentTypes_NoFinding()
    {
        // Same identity string but different component types is not a collision.
        WriteSolution("""
            <RootComponent type="1" schemaName="tp_warehouse" behavior="0" />
            <RootComponent type="300" schemaName="tp_warehouse" behavior="0" />
            """);
        WriteEntity("tp_warehouse");

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
    }

    [Fact]
    public void DistinctCanvasAppSchemaNames_NoFinding()
    {
        WriteSolution("""
            <RootComponent type="300" schemaName="tp_warehousepicking" behavior="0" />
            <RootComponent type="300" schemaName="tp_warehousereceiving" behavior="0" />
            """);

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
    }

    [Fact]
    public void DuplicateWorkflowId_ReportsError()
    {
        var workflowId = Guid.NewGuid();
        WriteSolution($$"""
            <RootComponent type="29" id="{{{workflowId}}}" behavior="0" />
            <RootComponent type="29" id="{{{workflowId}}}" behavior="0" />
            """);

        var finding = Assert.Single(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
        Assert.Contains(workflowId.ToString(), finding.Message);
    }

    [Fact]
    public void SideBySidePluginAssemblyVersions_NoFinding()
    {
        WriteSolution("""
            <RootComponent type="91" schemaName="Test.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" behavior="0" />
            <RootComponent type="91" schemaName="Test.Plugins, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null" behavior="0" />
            """);

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.DuplicateRootComponent);
    }

    private IReadOnlyList<ValidationResult> Validate()
    {
        var workspace = new XmlWorkspaceReader().Load(_root);
        return new SolutionManifestValidator().Validate(workspace);
    }

    private void WriteSolution(string rootComponents)
    {
        var otherDir = Path.Combine(_root, "Other");
        Directory.CreateDirectory(otherDir);

        File.WriteAllText(Path.Combine(otherDir, "Solution.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SolutionManifest>
                <UniqueName>TestSolution</UniqueName>
                <Version>1.0.0.0</Version>
                <Managed>0</Managed>
                <Publisher>
                  <UniqueName>TestPub</UniqueName>
                  <CustomizationPrefix>tp</CustomizationPrefix>
                  <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
                </Publisher>
                <RootComponents>
                  {rootComponents}
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);
    }

    private void WriteStep(string pluginTypeName)
    {
        var stepsDir = Path.Combine(_root, "SdkMessageProcessingSteps");
        Directory.CreateDirectory(stepsDir);

        File.WriteAllText(Path.Combine(stepsDir, "{01020304-0000-0000-0000-000000000007}.xml"), $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <SdkMessageProcessingStep SdkMessageProcessingStepId="{01020304-0000-0000-0000-000000000007}" Name="Test step">
              <SdkMessageId>{11111111-1111-1111-1111-111111111111}</SdkMessageId>
              <PluginTypeName>{{pluginTypeName}}</PluginTypeName>
              <PluginTypeId>{f1b2c3d4-0000-0000-0000-000000000006}</PluginTypeId>
              <Stage>20</Stage>
              <Mode>0</Mode>
              <Rank>1</Rank>
              <IntroducedVersion>1.0.0.0</IntroducedVersion>
              <IsCustomizable>1</IsCustomizable>
              <IsHidden>0</IsHidden>
            </SdkMessageProcessingStep>
            """);
    }

    private void WriteEntity(string logicalName)
    {
        var entityDir = Path.Combine(_root, "Entities", logicalName);
        Directory.CreateDirectory(entityDir);

        File.WriteAllText(Path.Combine(entityDir, "Entity.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity>
              <Name LocalizedName="{logicalName}" OriginalName="{logicalName}">{logicalName}</Name>
              <EntityInfo>
                <entity Name="{logicalName}">
                  <LocalizedNames>
                    <LocalizedName description="{logicalName}" languagecode="1033" />
                  </LocalizedNames>
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }

    private void WriteForm(string logicalName, Guid formId)
    {
        var formDir = Path.Combine(_root, "Entities", logicalName, "FormXml", "main");
        Directory.CreateDirectory(formDir);

        var bracedId = formId.ToString("B");

        File.WriteAllText(Path.Combine(formDir, $"{bracedId}.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <forms type="main">
              <systemform>
                <formid>{bracedId}</formid>
                <IsCustomizable>1</IsCustomizable>
                <form />
              </systemform>
            </forms>
            """);
    }
}
