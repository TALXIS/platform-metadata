using TALXIS.Platform.Metadata.Schema;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class JsonSchemaIntrospectorTests
{
    private readonly JsonSchemaIntrospector _introspector = new();

    [Fact]
    public void GetSchema_Flow_ReturnsStructure()
    {
        var schema = _introspector.GetSchema("Flow");

        Assert.NotNull(schema);
        Assert.Equal("Flow", schema!.RootElement);

        var topLevelNames = schema.Elements.Select(e => e.Name).ToList();
        Assert.Contains("properties", topLevelNames);
        Assert.Contains("schemaVersion", topLevelNames);

        // "properties" element should have children including "definition" and "connectionReferences"
        var propsElement = schema.Elements.First(e => e.Name == "properties");
        Assert.NotNull(propsElement.Children);

        var propsChildNames = propsElement.Children!.Select(e => e.Name).ToList();
        Assert.Contains("definition", propsChildNames);
        Assert.Contains("connectionReferences", propsChildNames);

        // "definition" should have nested children: parameters, triggers, actions
        var definition = propsElement.Children!.First(e => e.Name == "definition");
        Assert.NotNull(definition.Children);

        var defChildNames = definition.Children!.Select(e => e.Name).ToList();
        Assert.Contains("parameters", defChildNames);
        Assert.Contains("triggers", defChildNames);
        Assert.Contains("actions", defChildNames);

        // definition is required
        Assert.True(definition.Required);
    }

    [Fact]
    public void GetSchema_UnknownComponent_ReturnsNull()
    {
        var schema = _introspector.GetSchema("NonExistentComponent");

        Assert.Null(schema);
    }

    [Fact]
    public void GetSchema_Flow_CaseInsensitive()
    {
        var schema = _introspector.GetSchema("flow");

        Assert.NotNull(schema);
        Assert.Equal("flow", schema!.RootElement);
    }

    [Fact]
    public void GetSchema_Flow_TypeMappings()
    {
        var schema = _introspector.GetSchema("Flow");
        Assert.NotNull(schema);

        // "schemaVersion" should be typed as string
        var schemaVersion = schema!.Elements.First(e => e.Name == "schemaVersion");
        Assert.Equal("string", schemaVersion.TypeName);

        // "properties" should be typed as object
        var props = schema.Elements.First(e => e.Name == "properties");
        Assert.Equal("object", props.TypeName);
    }
}
