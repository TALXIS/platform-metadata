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
    public void MissingDependenciesAbsent_ReportsError()
    {
        WriteSolution(rootComponents: "", includeMissingDependencies: false);

        var results = Validate();

        var finding = Assert.Single(results, r => r.Code == ValidationDiagnostics.MissingDependenciesElementAbsent);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.EndsWith("Solution.xml", finding.FilePath);
    }

    [Fact]
    public void MissingDependenciesPresent_NoFinding()
    {
        WriteSolution(rootComponents: "", includeMissingDependencies: true);

        Assert.DoesNotContain(Validate(), r => r.Code == ValidationDiagnostics.MissingDependenciesElementAbsent);
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

    private IReadOnlyList<ValidationResult> Validate()
    {
        var workspace = new XmlWorkspaceReader().Load(_root);
        return new SolutionManifestValidator().Validate(workspace);
    }

    private void WriteSolution(string rootComponents, bool includeMissingDependencies = true)
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
                {(includeMissingDependencies ? "<MissingDependencies />" : "")}
              </SolutionManifest>
            </ImportExportXml>
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
