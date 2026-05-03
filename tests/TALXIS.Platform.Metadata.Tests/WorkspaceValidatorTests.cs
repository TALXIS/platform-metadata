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
}
