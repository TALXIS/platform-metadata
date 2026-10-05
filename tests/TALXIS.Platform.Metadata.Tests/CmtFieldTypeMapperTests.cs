using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtFieldTypeMapperTests
{
    [Theory]
    [InlineData(AttributeType.String, "string")]
    [InlineData(AttributeType.Memo, "string")]
    [InlineData(AttributeType.EntityName, "string")]
    [InlineData(AttributeType.Uniqueidentifier, "guid")]
    [InlineData(AttributeType.Integer, "number")]
    [InlineData(AttributeType.BigInt, "bigint")]
    [InlineData(AttributeType.Boolean, "bool")]
    [InlineData(AttributeType.DateTime, "datetime")]
    [InlineData(AttributeType.Decimal, "decimal")]
    [InlineData(AttributeType.Double, "float")]
    [InlineData(AttributeType.Money, "money")]
    [InlineData(AttributeType.Picklist, "optionsetvalue")]
    [InlineData(AttributeType.MultiSelectPicklist, "optionsetvaluecollection")]
    [InlineData(AttributeType.Lookup, "entityreference")]
    [InlineData(AttributeType.Customer, "entityreference")]
    [InlineData(AttributeType.Owner, "owner")]
    [InlineData(AttributeType.PartyList, "partylist")]
    [InlineData(AttributeType.State, "state")]
    [InlineData(AttributeType.Status, "status")]
    [InlineData(AttributeType.Image, "imagedata")]
    [InlineData(AttributeType.File, "filedata")]
    public void MapsAttributeTypeToCmtType(AttributeType attributeType, string expected)
    {
        Assert.Equal(expected, CmtFieldTypeMapper.ToCmtType(attributeType));
    }

    [Theory]
    [InlineData(AttributeType.Virtual)]
    [InlineData(AttributeType.ManagedProperty)]
    [InlineData(AttributeType.CalendarRules)]
    public void ReturnsNullForTypesCmtCannotMigrate(AttributeType attributeType)
    {
        Assert.Null(CmtFieldTypeMapper.ToCmtType(attributeType));
    }
}
