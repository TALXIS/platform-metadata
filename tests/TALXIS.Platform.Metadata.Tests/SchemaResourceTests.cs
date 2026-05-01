using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class SchemaResourceTests
{
    [Fact]
    public void AllSchemasAreEmbedded()
    {
        var schemas = SchemaResourceLoader.GetAvailableSchemas().ToList();
        Assert.Contains("Entity.xsd", schemas);
        Assert.Contains("Solution.xsd", schemas);
        Assert.Contains("Form.xsd", schemas);
        Assert.True(schemas.Count >= 23, $"Expected at least 23 schemas, got {schemas.Count}");
    }

    [Theory]
    [InlineData("Entity.xsd")]
    [InlineData("Solution.xsd")]
    [InlineData("Form.xsd")]
    [InlineData("Customizations.xsd")]
    public void SchemaCanBeOpened(string fileName)
    {
        using var stream = SchemaResourceLoader.OpenSchema(fileName);
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }
}
