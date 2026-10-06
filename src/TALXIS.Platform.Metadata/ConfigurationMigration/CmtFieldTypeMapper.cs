using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Maps Dataverse attribute types to the CMT field type vocabulary the way CMT's own schema generator does.
/// </summary>
public static class CmtFieldTypeMapper
{
    /// <summary>
    /// Returns the CMT field type for an attribute type, or <c>null</c> where CMT's generator would write the
    /// unimportable <see cref="CmtFieldTypes.Unknown"/>, so callers can leave the column out.
    /// </summary>
    public static string? ToCmtType(AttributeType attributeType) => attributeType switch
    {
        AttributeType.String or AttributeType.Memo or AttributeType.EntityName => CmtFieldTypes.String,
        AttributeType.Uniqueidentifier => CmtFieldTypes.Guid,
        AttributeType.Integer => CmtFieldTypes.Number,
        AttributeType.BigInt => CmtFieldTypes.BigInt,
        AttributeType.Boolean => CmtFieldTypes.Bool,
        AttributeType.DateTime => CmtFieldTypes.DateTime,
        AttributeType.Decimal => CmtFieldTypes.Decimal,
        AttributeType.Double => CmtFieldTypes.Float,
        AttributeType.Money => CmtFieldTypes.Money,
        AttributeType.Picklist => CmtFieldTypes.OptionSetValue,
        AttributeType.MultiSelectPicklist => CmtFieldTypes.OptionSetValueCollection,
        // CMT exports customer columns as entityreference with lookupType="account|contact".
        AttributeType.Lookup or AttributeType.Customer => CmtFieldTypes.EntityReference,
        AttributeType.Owner => CmtFieldTypes.Owner,
        AttributeType.PartyList => CmtFieldTypes.PartyList,
        AttributeType.State => CmtFieldTypes.State,
        AttributeType.Status => CmtFieldTypes.Status,
        AttributeType.Image => CmtFieldTypes.ImageData,
        AttributeType.File => CmtFieldTypes.FileData,
        _ => null
    };

    /// <summary>
    /// Returns the CMT field type for a column, telling owner lookups apart from plain and customer ones (<see cref="LookupKind"/>).
    /// </summary>
    public static string? ToCmtType(AttributeMetadata attribute) =>
        attribute is LookupAttributeMetadata { LookupKind: LookupKind.Owner } ? CmtFieldTypes.Owner : ToCmtType(attribute.AttributeType);
}
