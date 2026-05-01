using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.Tests;

public class AttributeMetadataTests
{
    [Fact]
    public void StringAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.Equal(AttributeType.String, attr.AttributeType);
    }

    [Fact]
    public void StringAttributeMetadata_HasDefaultMaxLength()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.Equal(100, attr.MaxLength);
    }

    [Fact]
    public void StringAttributeMetadata_HasDefaultFormatName()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.Equal(StringFormatName.Text, attr.FormatName);
    }

    [Fact]
    public void IntegerAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new IntegerAttributeMetadata { LogicalName = "count" };
        Assert.Equal(AttributeType.Integer, attr.AttributeType);
    }

    [Fact]
    public void BooleanAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new BooleanAttributeMetadata { LogicalName = "active" };
        Assert.Equal(AttributeType.Boolean, attr.AttributeType);
    }

    [Fact]
    public void BooleanAttributeMetadata_HasBooleanOptionSetMetadata()
    {
        var attr = new BooleanAttributeMetadata { LogicalName = "active" };
        Assert.NotNull(attr.OptionSet);
        Assert.IsType<BooleanOptionSetMetadata>(attr.OptionSet);
    }

    [Fact]
    public void PicklistAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new PicklistAttributeMetadata { LogicalName = "status" };
        Assert.Equal(AttributeType.Picklist, attr.AttributeType);
    }

    [Fact]
    public void PicklistAttributeMetadata_LinksToOptionSetMetadata()
    {
        var optionSet = new OptionSetMetadata { Name = "status_options" };
        var attr = new PicklistAttributeMetadata { LogicalName = "status", OptionSet = optionSet };

        Assert.Same(optionSet, attr.OptionSet);
    }

    [Fact]
    public void LookupAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new LookupAttributeMetadata { LogicalName = "parentid" };
        Assert.Equal(AttributeType.Lookup, attr.AttributeType);
    }

    [Fact]
    public void LookupAttributeMetadata_HasTargetsArray()
    {
        var attr = new LookupAttributeMetadata
        {
            LogicalName = "customerid",
            Targets = new[] { "account", "contact" }
        };

        Assert.Equal(2, attr.Targets.Length);
        Assert.Contains("account", attr.Targets);
        Assert.Contains("contact", attr.Targets);
    }

    [Fact]
    public void LookupAttributeMetadata_DefaultTargetsIsEmpty()
    {
        var attr = new LookupAttributeMetadata { LogicalName = "parentid" };
        Assert.Empty(attr.Targets);
    }

    [Fact]
    public void MoneyAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new MoneyAttributeMetadata { LogicalName = "revenue" };
        Assert.Equal(AttributeType.Money, attr.AttributeType);
    }

    [Fact]
    public void MoneyAttributeMetadata_HasDefaultPrecision()
    {
        var attr = new MoneyAttributeMetadata { LogicalName = "revenue" };
        Assert.Equal(2, attr.Precision);
    }

    [Fact]
    public void DecimalAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new DecimalAttributeMetadata { LogicalName = "rate" };
        Assert.Equal(AttributeType.Decimal, attr.AttributeType);
    }

    [Fact]
    public void DoubleAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new DoubleAttributeMetadata { LogicalName = "latitude" };
        Assert.Equal(AttributeType.Double, attr.AttributeType);
    }

    [Fact]
    public void DateTimeAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new DateTimeAttributeMetadata { LogicalName = "createdon" };
        Assert.Equal(AttributeType.DateTime, attr.AttributeType);
    }

    [Fact]
    public void MemoAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new MemoAttributeMetadata { LogicalName = "description" };
        Assert.Equal(AttributeType.Memo, attr.AttributeType);
    }

    [Fact]
    public void MemoAttributeMetadata_HasDefaultMaxLength()
    {
        var attr = new MemoAttributeMetadata { LogicalName = "description" };
        Assert.Equal(2000, attr.MaxLength);
    }

    [Fact]
    public void BigIntAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new BigIntAttributeMetadata { LogicalName = "versionnumber" };
        Assert.Equal(AttributeType.BigInt, attr.AttributeType);
    }

    [Fact]
    public void UniqueIdentifierAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new UniqueIdentifierAttributeMetadata { LogicalName = "accountid" };
        Assert.Equal(AttributeType.Uniqueidentifier, attr.AttributeType);
    }

    [Fact]
    public void StateAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new StateAttributeMetadata { LogicalName = "statecode" };
        Assert.Equal(AttributeType.State, attr.AttributeType);
    }

    [Fact]
    public void StatusAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new StatusAttributeMetadata { LogicalName = "statuscode" };
        Assert.Equal(AttributeType.Status, attr.AttributeType);
    }

    [Fact]
    public void MultiSelectPicklistAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new MultiSelectPicklistAttributeMetadata { LogicalName = "tags" };
        Assert.Equal(AttributeType.MultiSelectPicklist, attr.AttributeType);
    }

    [Fact]
    public void ImageAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new ImageAttributeMetadata { LogicalName = "entityimage" };
        Assert.Equal(AttributeType.Image, attr.AttributeType);
    }

    [Fact]
    public void FileAttributeMetadata_ReturnsCorrectType()
    {
        var attr = new FileAttributeMetadata { LogicalName = "document" };
        Assert.Equal(AttributeType.File, attr.AttributeType);
    }

    [Fact]
    public void AttributeMetadata_DefaultRequiredLevel_IsNone()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.Equal(RequiredLevel.None, attr.RequiredLevel);
    }

    [Fact]
    public void AttributeMetadata_DefaultIsSearchable_IsTrue()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.True(attr.IsSearchable);
    }

    [Fact]
    public void AttributeMetadata_SourceLocation_InheritedFromMetadataBase()
    {
        var attr = new StringAttributeMetadata { LogicalName = "name" };
        Assert.Null(attr.Source);

        attr.Source = new SourceLocation("attributes.xml", 5, 3);
        Assert.Equal("attributes.xml", attr.Source.FilePath);
    }
}
