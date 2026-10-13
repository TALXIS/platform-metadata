using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionValidatorTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void Validate_NonExistentPath_ReturnsError()
    {
        var report = new SolutionValidator().Validate("/nonexistent/path/that/does/not/exist");

        Assert.True(report.ErrorCount > 0);
        Assert.Null(report.Workspace);
        Assert.Contains(report.Results, r =>
            r.Severity == ValidationSeverity.Error && r.Message.Contains("Directory not found"));
    }

    [Fact]
    public void Validate_SampleSolution_LoadsWorkspaceAndSummary()
    {
        var report = new SolutionValidator().Validate(SamplePath);

        Assert.NotNull(report.Workspace);
        Assert.NotNull(report.LoadedComponents);
        Assert.True(report.LoadedComponents.Total > 0);
        Assert.DoesNotContain(report.Results, r => r.Message.Contains("No Other/Solution.xml"));
    }

    [Fact]
    public void Validate_MissingSolutionManifest_ReportsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sol-test-manifest-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var report = new SolutionValidator().Validate(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Warning && r.Message.Contains("No Other/Solution.xml"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CmtDataSchemaRules_NotPartOfSolutionValidation()
    {
        // CMT packages ship in PD packages, never inside a solution, so the
        // updateCompare rule belongs to workspace/pd-package validation only.
        var tempDir = Path.Combine(Path.GetTempPath(), $"sol-test-cmt-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
            File.WriteAllText(Path.Combine(tempDir, "Data", "data_schema.xml"), """
                <entities>
                  <entity name="talxis_configuration">
                    <fields>
                      <field name="talxis_configurationid" type="guid" primaryKey="true" />
                    </fields>
                  </entity>
                </entities>
                """);

            var report = new SolutionValidator().Validate(tempDir);

            Assert.DoesNotContain(report.Results, r => r.Code == ValidationDiagnostics.CmtEntityMissingUpdateCompare);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RelationshipRules_NotPartOfSolutionValidation()
    {
        var ws = new Workspace("test");
        var entity = new EntityMetadata { LogicalName = "pba_child" };
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "pba_lookup", IsCustomAttribute = true });
        ws.AddEntity(entity);
        ws.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "pba_parent_pba_child",
            ReferencedEntity = "pba_parent",
            ReferencedAttribute = "pba_parentid",
            ReferencingEntity = "pba_child",
            ReferencingAttribute = "pba_missing",
        });

        var missingDir = Path.Combine(Path.GetTempPath(), $"sol-test-none-{Guid.NewGuid():N}");
        var results = new SolutionValidator().Validate(ws, missingDir).Results;

        Assert.DoesNotContain(results, r => r.Message.Contains("pba_missing"));
        Assert.DoesNotContain(results, r => r.Message.Contains("[Relationship]"));
    }

    // Solution component instances (solution-aware table records with Individual file scope, SCF components)
    // unpack to <folder>/<key>/<name>.xml. Root element and columns come from the table definition, not an XSD.
    private const string WidgetRecordXml = """
        <tst_widget tst_unique_name="tst_default_widget">
          <tst_name>Default widget</tst_name>
          <tst_widgetid>6f4d6c2e-0d1b-4f43-9a51-2a0c1b7e3c10</tst_widgetid>
          <tst_parentid>
            <tst_unique_name>tst_parent</tst_unique_name>
          </tst_parentid>
          <statecode>0</statecode>
          <statuscode>1</statuscode>
        </tst_widget>
        """;

    [Fact]
    public void SolutionAwareRecordFile_IsNotSchemaValidated()
    {
        var results = ValidateFiles(("tst_widgets/tst_default_widget/tst_widget.xml", WidgetRecordXml));

        Assert.DoesNotContain(results, r => r.FilePath != null && r.FilePath.EndsWith("tst_widget.xml"));
    }

    [Fact]
    public void ScfComponentFile_WithExtraAttributesAndSiblingFiles_IsNotSchemaValidated()
    {
        var results = ValidateFiles(
            ("bots/tst_assistant/bot.xml", """<bot schemaname="tst_assistant" componenttype="0"><name>Assistant</name></bot>"""),
            ("bots/tst_assistant/configuration.json", "{}"));

        Assert.DoesNotContain(results, r => r.FilePath != null && r.FilePath.Contains("tst_assistant"));
    }

    [Theory]
    [InlineData("tst_widgets/tst_default_widget/tst_gadget.xml", "root element differs from the file name")]
    [InlineData("tst_widgets/tst_other_widget/tst_widget.xml", "no root attribute matches the folder")]
    [InlineData("tst_widgets/tst_widget.xml", "not in a <folder>/<key> subfolder")]
    [InlineData("tst_more/tst_widgets/tst_default_widget/tst_widget.xml", "nested deeper than <folder>/<key>")]
    public void UndeclaredRootOutsideComponentInstanceShape_ReportsNoSchemaWarning(string path, string because)
    {
        var results = ValidateFiles((path, WidgetRecordXml));

        var finding = Assert.Single(results, r => r.FilePath != null && r.FilePath.EndsWith(Path.GetFileName(path)));
        Assert.True(finding.Severity == ValidationSeverity.Warning, because);
        Assert.Equal(ValidationDiagnostics.SchemaNotFound, finding.Code);
    }

    [Fact]
    public void ComponentInstanceShapedFile_WithDeclaredRoot_IsStillSchemaValidated()
    {
        // The option set root is declared by OptionSet.xsd, so matching the component instance shape
        // must not exempt it from validation.
        var results = ValidateFiles(("tst_choices/tst_color/optionset.xml", """<optionset Name="tst_color"><tst_bogus /></optionset>"""));

        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error && r.FilePath != null && r.FilePath.EndsWith("optionset.xml"));
    }

    private static IReadOnlyList<ValidationResult> ValidateFiles(params (string RelativePath, string Content)[] files)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sol-test-files-{Guid.NewGuid():N}");
        try
        {
            foreach (var (relativePath, content) in files)
            {
                var fullPath = Path.Combine(tempDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content);
            }

            return new SolutionValidator().Validate(tempDir).Results
                .Where(r => r.Stage == ValidationStage.Schema || r.Stage == ValidationStage.Json)
                .ToList();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
