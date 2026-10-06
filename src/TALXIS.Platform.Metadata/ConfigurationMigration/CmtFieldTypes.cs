namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// The CMT <c>type</c> vocabulary for schema fields. Values are lowercase because CMT's importer compares them
/// ordinally. <see cref="Unknown"/> is written by CMT's generator but has no import conversion, <see cref="BigInt"/>
/// is accepted but its values are dropped, <see cref="Customer"/> is rejected (Customer columns are
/// <see cref="EntityReference"/>), and <see cref="File"/> is a TALXIS synonym for <see cref="FileData"/> that Microsoft CMT rejects.
/// </summary>
public static class CmtFieldTypes
{
    public const string String = "string";
    public const string Guid = "guid";
    public const string Number = "number";
    public const string BigInt = "bigint";
    public const string Bool = "bool";
    public const string DateTime = "datetime";
    public const string Decimal = "decimal";
    public const string Float = "float";
    public const string Money = "money";
    public const string OptionSetValue = "optionsetvalue";
    public const string OptionSetValueCollection = "optionsetvaluecollection";
    public const string EntityReference = "entityreference";

    /// <summary>Rejected by CMT import and export; a Customer column is <see cref="EntityReference"/> with lookupType <c>account|contact</c>.</summary>
    public const string Customer = "customer";
    public const string Owner = "owner";
    public const string PartyList = "partylist";
    public const string State = "state";
    public const string Status = "status";
    public const string ImageData = "imagedata";
    public const string FileData = "filedata";

    /// <summary>Emitted by CMT's generator for attribute types it cannot map; not importable.</summary>
    public const string Unknown = "unknown";

    /// <summary>TALXIS dialect synonym for <see cref="FileData"/>; not accepted by Microsoft CMT.</summary>
    public const string File = "file";

    /// <summary>Every type Microsoft CMT can import, in the spelling it requires. Excludes <see cref="BigInt"/>, <see cref="Customer"/>, <see cref="Unknown"/> and the TALXIS synonym <see cref="File"/>.</summary>
    public static readonly IReadOnlyCollection<string> Importable = new HashSet<string>(StringComparer.Ordinal)
    {
        String, Guid, Number, Bool, DateTime, Decimal, Float, Money, OptionSetValue, OptionSetValueCollection,
        EntityReference, Owner, PartyList, State, Status, ImageData, FileData
    };
}
