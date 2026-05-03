using TALXIS.Platform.Metadata.Schema;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class XsdSchemaIntrospectorTests
{
    private readonly XsdSchemaIntrospector _introspector = new();

    [Fact]
    public void GetSchema_Entity_ReturnsStructure()
    {
        var schema = _introspector.GetSchema("Entity");

        Assert.NotNull(schema);
        Assert.Equal("Entity", schema.RootElement);

        var elementNames = schema.Elements.Select(e => e.Name).ToList();
        Assert.Contains("Name", elementNames);
        Assert.Contains("EntityInfo", elementNames);
    }

    [Fact]
    public void GetSchema_Role_ReturnsPrivileges()
    {
        var schema = _introspector.GetSchema("Role");

        Assert.NotNull(schema);
        Assert.Equal("Role", schema.RootElement);

        var elementNames = schema.Elements.Select(e => e.Name).ToList();
        Assert.Contains("RolePrivileges", elementNames);

        Assert.NotNull(schema.Attributes);
        var attrNames = schema.Attributes.Select(a => a.Name).ToList();
        Assert.Contains("name", attrNames);
        Assert.Contains("id", attrNames);
    }

    [Fact]
    public void GetSchema_UnknownElement_ReturnsNull()
    {
        var schema = _introspector.GetSchema("NonExistentElement");

        Assert.Null(schema);
    }

    [Fact]
    public void GetSchema_OptionSet_HasEnumValues()
    {
        // The Entity schema uses TrueFalse01Type which has enumeration values "0" and "1".
        // We check this through the Entity root since optionset root may not directly expose it.
        var schema = _introspector.GetSchema("Entity");
        Assert.NotNull(schema);

        // Walk into EntityInfo to find an element that uses TrueFalse01Type
        var entityInfo = schema.Elements.FirstOrDefault(e => e.Name == "EntityInfo");
        Assert.NotNull(entityInfo);
        Assert.NotNull(entityInfo.Children);

        // EntityInfo -> entity -> fields like IsConnectionsEnabled use TrueFalse01Type
        var entity = entityInfo.Children.FirstOrDefault(e => e.Name == "entity");
        Assert.NotNull(entity);
        Assert.NotNull(entity.Children);

        var trueFalse01Element = entity.Children.FirstOrDefault(e =>
            e.AllowedValues != null &&
            e.AllowedValues.Contains("0") &&
            e.AllowedValues.Contains("1"));

        Assert.NotNull(trueFalse01Element);
        Assert.Equal("enum", trueFalse01Element.TypeName);
        Assert.Equal(new[] { "0", "1" }, trueFalse01Element.AllowedValues);
    }

    [Fact]
    public void GetSchema_Form_HasTabs()
    {
        var schema = _introspector.GetSchema("form");

        Assert.NotNull(schema);
        Assert.Equal("form", schema.RootElement);

        var elementNames = schema.Elements.Select(e => e.Name).ToList();
        Assert.Contains("tabs", elementNames);

        // tabs -> tab -> columns -> column -> sections
        var tabs = schema.Elements.First(e => e.Name == "tabs");
        Assert.NotNull(tabs.Children);

        var tab = tabs.Children.FirstOrDefault(e => e.Name == "tab");
        Assert.NotNull(tab);
        Assert.NotNull(tab.Children);

        var columns = tab.Children.FirstOrDefault(e => e.Name == "columns");
        Assert.NotNull(columns);
        Assert.NotNull(columns.Children);

        var column = columns.Children.FirstOrDefault(e => e.Name == "column");
        Assert.NotNull(column);
        Assert.NotNull(column.Children);

        var sections = column.Children.FirstOrDefault(e => e.Name == "sections");
        Assert.NotNull(sections);
    }

    [Fact]
    public void GetSchemaForComponentType_Entity_ReturnsSameAsGetSchema()
    {
        var byType = _introspector.GetSchemaForComponentType(ComponentType.Entity);
        var byName = _introspector.GetSchema("Entity");

        Assert.NotNull(byType);
        Assert.NotNull(byName);
        Assert.Equal(byType.RootElement, byName.RootElement);
        Assert.Equal(byType.Elements.Count, byName.Elements.Count);
    }
}
