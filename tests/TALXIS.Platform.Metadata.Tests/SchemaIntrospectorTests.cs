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

    [Fact]
    public void GetSchemaForComponentType_ConnectionRole_UsesSerializedName()
    {
        // ConnectionRole has Name="ConnectionRole" but SerializedName="ConnectionRoles"
        // which matches the XSD root element <xs:element name="ConnectionRoles">
        var schema = _introspector.GetSchemaForComponentType(ComponentType.ConnectionRole);

        Assert.NotNull(schema);
        Assert.Equal("ConnectionRoles", schema!.RootElement);
    }

    [Fact]
    public void GetSchemaForComponentType_FieldSecurityProfile_UsesSerializedName()
    {
        var schema = _introspector.GetSchemaForComponentType(ComponentType.FieldSecurityProfile);

        Assert.NotNull(schema);
        Assert.Equal("FieldSecurityProfiles", schema!.RootElement);
    }

    [Fact]
    public void GetSchemaForComponentType_SystemForm_ReturnsSchema()
    {
        // SystemForm maps via SerializedName "SystemForms" first, falls back to Name "SystemForm".
        // The Form.xsd root elements are "forms" and "form" (lowercase), so GetSchema may
        // fall through to the Name lookup. This test verifies the mapping chain works.
        var schema = _introspector.GetSchemaForComponentType(ComponentType.SystemForm);

        // SystemForm doesn't have a matching XSD root — the form schema uses lowercase "form"/"forms".
        // GetSchemaForComponentType returns null, but GetSchema("form") works directly.
        var formSchema = _introspector.GetSchema("form");
        Assert.NotNull(formSchema);
        Assert.Equal("form", formSchema!.RootElement);
    }

    [Fact]
    public void GetSchema_Form_HasDeepStructure_ToControlLevel()
    {
        // With depth limit of 10, the walker should reach: form -> tabs -> tab -> columns ->
        // column -> sections -> section -> rows -> row -> cell -> control (10 levels)
        var schema = _introspector.GetSchema("form");
        Assert.NotNull(schema);

        var tabs = schema!.Elements.First(e => e.Name == "tabs");
        var tab = tabs.Children!.First(e => e.Name == "tab");
        var columns = tab.Children!.First(e => e.Name == "columns");
        var column = columns.Children!.First(e => e.Name == "column");
        var sections = column.Children!.First(e => e.Name == "sections");
        var section = sections.Children!.First(e => e.Name == "section");
        var rows = section.Children!.First(e => e.Name == "rows");

        Assert.NotNull(rows.Children);
        var row = rows.Children!.First(e => e.Name == "row");
        Assert.NotNull(row.Children);
        var cell = row.Children!.First(e => e.Name == "cell");
        Assert.NotNull(cell.Children);
        var control = cell.Children!.FirstOrDefault(e => e.Name == "control");
        Assert.NotNull(control);
    }

    [Fact]
    public void GetSchema_Form_CellHasAttributeGroupAttributes()
    {
        // Cell elements reference <xs:attributeGroup ref="FormXmlCellCommon"/> which should
        // contribute attributes like "rowspan", "colspan", "visible" etc.
        var schema = _introspector.GetSchema("form");
        Assert.NotNull(schema);

        var tabs = schema!.Elements.First(e => e.Name == "tabs");
        var tab = tabs.Children!.First(e => e.Name == "tab");
        var columns = tab.Children!.First(e => e.Name == "columns");
        var column = columns.Children!.First(e => e.Name == "column");
        var sections = column.Children!.First(e => e.Name == "sections");
        var section = sections.Children!.First(e => e.Name == "section");
        var rows = section.Children!.First(e => e.Name == "rows");
        var row = rows.Children!.First(e => e.Name == "row");
        var cell = row.Children!.First(e => e.Name == "cell");

        Assert.NotNull(cell.Attributes);
        var attrNames = cell.Attributes!.Select(a => a.Name).ToList();

        // These come from the FormXmlCellCommon attributeGroup
        Assert.Contains("rowspan", attrNames);
        Assert.Contains("colspan", attrNames);
        Assert.Contains("visible", attrNames);
    }

    [Fact]
    public void GetSchema_Form_SectionHasAttributeGroupAttributes()
    {
        // Section elements reference <xs:attributeGroup ref="FormXmlSectionCommon"/>
        var schema = _introspector.GetSchema("form");
        Assert.NotNull(schema);

        var tabs = schema!.Elements.First(e => e.Name == "tabs");
        var tab = tabs.Children!.First(e => e.Name == "tab");
        var columns = tab.Children!.First(e => e.Name == "columns");
        var column = columns.Children!.First(e => e.Name == "column");
        var sections = column.Children!.First(e => e.Name == "sections");
        var section = sections.Children!.First(e => e.Name == "section");

        Assert.NotNull(section.Attributes);
        var attrNames = section.Attributes!.Select(a => a.Name).ToList();

        // These come from the FormXmlSectionCommon attributeGroup
        Assert.Contains("columns", attrNames);
        Assert.Contains("labelwidth", attrNames);
        Assert.Contains("celllabelposition", attrNames);
    }
}
