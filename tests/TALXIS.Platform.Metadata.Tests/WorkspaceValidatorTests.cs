using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class WorkspaceValidatorTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void ValidateDirectory_NonExistentPath_ReturnsError()
    {
        var validator = new WorkspaceValidator();
        var report = validator.ValidateDirectory("/nonexistent/path/that/does/not/exist");

        Assert.True(report.ErrorCount > 0);
        Assert.Null(report.Workspace);
        Assert.Null(report.LoadedComponents);
        Assert.Contains(report.Results, r =>
            r.Severity == ValidationSeverity.Error && r.Message.Contains("Directory not found"));
    }

    [Fact]
    public void ValidateDirectory_SampleWorkspace_ReturnsReportWithWorkspaceAndSummary()
    {
        var validator = new WorkspaceValidator();
        var report = validator.ValidateDirectory(SamplePath);

        Assert.NotNull(report.Workspace);
        Assert.NotNull(report.LoadedComponents);
        Assert.True(report.LoadedComponents.Total > 0,
            "Expected at least one component to be loaded from SampleWorkspace");
    }

    [Fact]
    public void SchemaError_SurfacedInReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-schema-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "Entity.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Entity><InvalidElementThatShouldNotExist /></Entity>");

            var validator = new WorkspaceValidator();
            var report = validator.ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error || r.Severity == ValidationSeverity.Warning);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DuplicateGuid_SurfacedInReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-guid-{Guid.NewGuid():N}");
        try
        {
            var formDir = Path.Combine(tempDir, "Entities", "test_entity", "FormXml", "main");
            Directory.CreateDirectory(formDir);

            var duplicateGuid = "{12345678-1234-1234-1234-123456789012}";
            File.WriteAllText(Path.Combine(formDir, "Form1.xml"),
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<forms><systemform><formid>{duplicateGuid}</formid></systemform></forms>");
            File.WriteAllText(Path.Combine(formDir, "Form2.xml"),
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<forms><systemform><formid>{duplicateGuid}</formid></systemform></forms>");

            var validator = new WorkspaceValidator();
            var report = validator.ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error && r.Message.Contains("Duplicate"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadError_SurfacedAsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-load-{Guid.NewGuid():N}");
        try
        {
            var entityDir = Path.Combine(tempDir, "Entities", "broken_entity");
            Directory.CreateDirectory(entityDir);
            // Malformed XML that the reader cannot parse
            File.WriteAllText(Path.Combine(entityDir, "Entity.xml"), "<broken");

            var validator = new WorkspaceValidator();
            var report = validator.ValidateDirectory(tempDir);

            // Malformed XML must surface as errors, never just warnings
            Assert.True(report.ErrorCount > 0,
                "Expected at least one error for malformed XML");
            Assert.DoesNotContain(report.Results, r =>
                r.Severity == ValidationSeverity.Warning && r.Message.Contains("Load error"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void InvalidFlowJson_SurfacedWithLineAndColumn()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-flow-json-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Workflows"));
            File.WriteAllText(Path.Combine(tempDir, "Workflows", "broken-flow.json"), "{\n  \"properties\": {\n");

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.FilePath != null &&
                r.FilePath.EndsWith("broken-flow.json", StringComparison.Ordinal) &&
                r.Line > 0 &&
                r.Column > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FlowDiagnostics_SurfacedInReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-flow-diagnostics-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "Workflows"));
            File.WriteAllText(Path.Combine(tempDir, "Workflows", "broken-flow.json"), """
                {
                  "properties": {
                    "connectionReferences": {},
                    "definition": {
                      "triggers": {
                        "Request": {
                          "type": "Request"
                        }
                      },
                      "actions": {
                        "Compose": {
                          "type": "Compose",
                          "runAfter": {
                            "Missing_action": [
                              "Succeeded"
                            ]
                          }
                        }
                      }
                    }
                  }
                }
                """);

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.Message.Contains("FLOW009", StringComparison.Ordinal) &&
                r.FilePath != null &&
                r.FilePath.EndsWith("broken-flow.json", StringComparison.Ordinal) &&
                r.Line > 0 &&
                r.Column > 0);
            Assert.NotNull(report.Workspace);
            Assert.Single(report.Workspace.FlowDefinitions);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateDirectory_RelationshipFindings_StillIncluded()
    {
        var report = new WorkspaceValidator().ValidateDirectory(SamplePath);

        Assert.Contains(report.Results, r => r.Message.Contains("[Relationship]"));
    }

    [Fact]
    public void ValidateRelationships_ReportsOnlyRelationshipStage()
    {
        var report = new WorkspaceValidator().ValidateRelationships(SamplePath);

        Assert.Contains(report.Results, r => r.Message.Contains("[Relationship]"));
        Assert.DoesNotContain(report.Results, r => r.Message.Contains("[Schema]"));
        Assert.DoesNotContain(report.Results, r => r.Message.Contains("[Solution]"));
    }

    [Fact]
    public void MultiSolution_DuplicateGuidAcrossRoots_DifferentComponents_ReportsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-xroot-guid-{Guid.NewGuid():N}");
        try
        {
            var guid = "{12345678-1234-1234-1234-123456789012}";
            WriteSolution(tempDir, "SolA", ("Entities", "entity_one", "Form1.xml", guid));
            WriteSolution(tempDir, "SolB", ("Entities", "entity_two", "Form2.xml", guid));

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error && r.Message.Contains("Duplicate GUID"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MultiSolution_SameComponentInBothRoots_NotFlaggedAsDuplicate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-xroot-layer-{Guid.NewGuid():N}");
        try
        {
            var guid = "{12345678-1234-1234-1234-123456789012}";
            WriteSolution(tempDir, "SolA", ("Entities", "shared_entity", "Form1.xml", guid));
            WriteSolution(tempDir, "SolB", ("Entities", "shared_entity", "Form1.xml", guid));

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.DoesNotContain(report.Results, r => r.Message.Contains("Duplicate GUID"));
            Assert.NotNull(report.Workspace);
            Assert.NotNull(report.LoadedComponents);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MultiSolution_OneSolutionFailsToLoad_OthersStillValidated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-partial-load-{Guid.NewGuid():N}");
        try
        {
            WriteSolution(tempDir, "SolBroken");
            var brokenEntityDir = Path.Combine(tempDir, "SolBroken", "Entities", "broken_entity");
            Directory.CreateDirectory(brokenEntityDir);
            File.WriteAllText(Path.Combine(brokenEntityDir, "Entity.xml"), "<broken");

            WriteSolution(tempDir, "SolHealthy");
            var manifestPath = Path.Combine(tempDir, "SolHealthy", "Other", "Solution.xml");
            File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace(
                "<RootComponents />",
                "<RootComponents><RootComponent type=\"1\" schemaName=\"tp_missing_entity\" behavior=\"0\" /></RootComponents>"));

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.Message.Contains("Failed to load workspace into model") &&
                r.FilePath != null && r.FilePath.Contains("SolBroken"));
            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.Message.Contains("tp_missing_entity"));
            Assert.Null(report.Workspace);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MultiSolution_FileOutsideAnyRoot_StillValidated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-leftover-{Guid.NewGuid():N}");
        try
        {
            WriteSolution(tempDir, "SolA");
            File.WriteAllText(Path.Combine(tempDir, "stray.xml"), "<broken");

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.FilePath != null && r.FilePath.EndsWith("stray.xml", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MultiSolution_DuplicateGuidWithinOneRoot_StillReported()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-test-xroot-intra-{Guid.NewGuid():N}");
        try
        {
            var guid = "{12345678-1234-1234-1234-123456789012}";
            WriteSolution(tempDir, "SolA",
                ("Entities", "entity_one", "Form1.xml", guid),
                ("Entities", "entity_two", "Form2.xml", guid));
            WriteSolution(tempDir, "SolB", ("Entities", "entity_other", "Form3.xml", "{aaaaaaaa-bbbb-cccc-dddd-eeeeffff0000}"));

            var report = new WorkspaceValidator().ValidateDirectory(tempDir);

            Assert.Contains(report.Results, r =>
                r.Severity == ValidationSeverity.Error && r.Message.Contains("Duplicate GUID"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static void WriteSolution(string workspaceDir, string uniqueName, params (string Folder, string Entity, string FormFile, string FormId)[] forms)
    {
        var solutionDir = Path.Combine(workspaceDir, uniqueName);
        Directory.CreateDirectory(Path.Combine(solutionDir, "Other"));
        File.WriteAllText(Path.Combine(solutionDir, "Other", "Solution.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SolutionManifest>
                <UniqueName>{uniqueName}</UniqueName>
                <LocalizedNames>
                  <LocalizedName description="{uniqueName}" languagecode="1033" />
                </LocalizedNames>
                <Descriptions />
                <Version>1.0.0.0</Version>
                <Managed>0</Managed>
                <Publisher>
                  <UniqueName>TestPub</UniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Test Publisher" languagecode="1033" />
                  </LocalizedNames>
                  <Descriptions />
                  <EMailAddress xsi:nil="true" />
                  <SupportingWebsiteUrl xsi:nil="true" />
                  <CustomizationPrefix>tp</CustomizationPrefix>
                  <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
                  <Addresses />
                </Publisher>
                <RootComponents />
                <MissingDependencies />
              </SolutionManifest>
            </ImportExportXml>
            """);

        foreach (var (folder, entity, formFile, formId) in forms)
        {
            var formDir = Path.Combine(solutionDir, folder, entity, "FormXml", "main");
            Directory.CreateDirectory(formDir);
            File.WriteAllText(Path.Combine(formDir, formFile),
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<forms><systemform><formid>{formId}</formid></systemform></forms>");
        }
    }
}
