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
}
