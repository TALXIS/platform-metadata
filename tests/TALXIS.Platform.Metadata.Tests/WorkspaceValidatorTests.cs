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
}
