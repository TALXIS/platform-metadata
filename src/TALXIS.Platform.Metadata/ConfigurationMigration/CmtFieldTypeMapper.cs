using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Maps Dataverse attribute types to the CMT field type vocabulary, following what CMT's own schema generator
/// emits (validated against 454 fields of real exports; Customer columns come out as <c>entityreference</c>
/// with a pipe-joined lookupType, not as <c>customer</c>).
/// </summary>
public static class CmtFieldTypeMapper
{
    /// <summary>
    /// Returns the CMT field type for an attribute type, or <c>null</c> when CMT has no type for it
    /// (Virtual, ManagedProperty, CalendarRules). CMT's generator writes <see cref="CmtFieldTypes.Unknown"/>
    /// in that case; this mapper returns <c>null</c> so callers can leave the column out instead of emitting a
    /// type the importer rejects. <see cref="CmtFieldTypes.BigInt"/> is returned for BigInt although CMT
    /// cannot import it either (TXM016 reports both).
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
        _ => null,
    };
}
