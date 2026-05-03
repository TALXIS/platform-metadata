using Newtonsoft.Json.Schema;
using TALXIS.Platform.Metadata.Validation;
using ValidationJsonValidator = TALXIS.Platform.Metadata.Validation.JsonValidator;

namespace TALXIS.Platform.Metadata.Tests;

public class JsonValidatorTests
{
    [Fact]
    public void ValidateFile_InvalidJson_ReturnsLineAndColumn()
    {
        WithJsonFile("broken.json", "{\n  \"properties\": {\n", file =>
        {
            var result = new ValidationJsonValidator().ValidateFile(file).Single();

            Assert.Equal(ValidationSeverity.Error, result.Severity);
            Assert.Equal(file, result.FilePath);
            Assert.True(result.Line > 0);
            Assert.True(result.Column > 0);
        });
    }

    [Fact]
    public void ValidateFile_ValidFlowSchema_ReturnsNoErrors()
    {
        WithJsonFile("flow.json", """
            {
              "properties": {
                "connectionReferences": {},
                "definition": {
                  "parameters": {},
                  "triggers": {},
                  "actions": {}
                }
              },
              "schemaVersion": "1.0.0.0"
            }
            """, file =>
        {
            var results = new ValidationJsonValidator().ValidateFile(file);

            Assert.Empty(results);
        });
    }

    [Fact]
    public void ValidateFile_ValidAgainstAnySchema_ReturnsNoErrors()
    {
        var validator = new ValidationJsonValidator(new[]
        {
            JSchema.Parse("""
                {
                  "type": "object",
                  "required": ["first"],
                  "properties": {
                    "first": { "type": "string" }
                  }
                }
                """),
            JSchema.Parse("""
                {
                  "type": "object",
                  "required": ["second"],
                  "properties": {
                    "second": { "type": "string" }
                  }
                }
                """)
        });

        WithJsonFile("multi-schema.json", """
            {
              "second": "ok"
            }
            """, file =>
        {
            var results = validator.ValidateFile(file);

            Assert.Empty(results);
        });
    }

    [Fact]
    public void ValidateFile_InvalidAgainstAllSchemas_AggregatesErrors()
    {
        var validator = new ValidationJsonValidator(new[]
        {
            JSchema.Parse("""
                {
                  "type": "object",
                  "required": ["first"],
                  "properties": {
                    "first": { "type": "string" }
                  }
                }
                """),
            JSchema.Parse("""
                {
                  "type": "object",
                  "required": ["second"],
                  "properties": {
                    "second": { "type": "string" }
                  }
                }
                """)
        });

        WithJsonFile("multi-schema.json", """
            {
              "other": "value"
            }
            """, file =>
        {
            var results = validator.ValidateFile(file);

            Assert.Equal(2, results.Count);
            Assert.All(results, result =>
            {
                Assert.Equal(ValidationSeverity.Error, result.Severity);
                Assert.Equal(file, result.FilePath);
                Assert.Contains("Required properties are missing", result.Message);
            });
        });
    }

    [Fact]
    public void ValidateFile_InvalidFlowSchema_ReturnsLineAndColumn()
    {
        WithJsonFile("flow.json", """
            {
              "properties": {
                "connectionReferences": {}
              }
            }
            """, file =>
        {
            var results = new ValidationJsonValidator().ValidateFile(file);

            Assert.NotEmpty(results);
            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error &&
                r.FilePath == file &&
                r.Line > 0 &&
                r.Column > 0);
        });
    }

    private static void WithJsonFile(string fileName, string content, Action<string> assertion)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"json-validator-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, fileName);
            File.WriteAllText(file, content);
            assertion(file);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
