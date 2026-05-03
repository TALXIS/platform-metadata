using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class JsonValidatorTests
{
    [Fact]
    public void ValidateFile_InvalidJson_ReturnsLineAndColumn()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"json-validator-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "broken.json");
            File.WriteAllText(file, "{\n  \"properties\": {\n");

            var result = new JsonValidator().ValidateFile(file).Single();

            Assert.Equal(ValidationSeverity.Error, result.Severity);
            Assert.Equal(file, result.FilePath);
            Assert.True(result.Line > 0);
            Assert.True(result.Column > 0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void ValidateFile_InvalidFlowSchema_ReturnsLineAndColumn()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"json-validator-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "flow.json");
            File.WriteAllText(file, string.Join(Environment.NewLine, new[]
            {
                "{",
                "  \"properties\": {",
                "    \"connectionReferences\": {}",
                "  }",
                "}"
            }));

            var results = new JsonValidator().ValidateFile(file);

            Assert.NotEmpty(results);
            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.FilePath == file &&
                r.Line > 0 &&
                r.Column > 0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
