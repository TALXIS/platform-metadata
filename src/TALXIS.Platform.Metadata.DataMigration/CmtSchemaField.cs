namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// One <c>&lt;field&gt;</c> of a data_schema.xml entity (or of a many-to-many relationship's nested fields).
/// The three boolean flags model CMT's "absent means false": the writer never emits <c>false</c> for them.
/// </summary>
public sealed class CmtSchemaField : MetadataBase
{
    /// <summary>Column logical name.</summary>
    public required string Name { get; set; }

    /// <summary>Display name. Written by CMT's generator, ignored by both importers; absent on M2M intersect fields.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// CMT type name, lowercase, from the <see cref="CmtFieldTypes"/> vocabulary (for example <c>string</c>,
    /// <c>entityreference</c>, <c>optionsetvalue</c>). CMT compares it case-sensitively and has no conversion for
    /// <c>bigint</c> or <c>unknown</c>.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>Marks the primary id column (<c>primaryKey="true"</c>). Absent means false; the writer never emits false.</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>Includes the column in the natural key CMT uses to match existing records (<c>updateCompare="true"</c>). Absent means false; the writer never emits false.</summary>
    public bool IsUpdateCompare { get; set; }

    /// <summary>Marks a custom column (<c>customfield="true"</c>); informational. Absent means false; the writer never emits false.</summary>
    public bool IsCustomField { get; set; }

    /// <summary>
    /// For entityreference columns: the target table logical names joined with <c>|</c> (for example
    /// <c>account|contact</c>), or <c>*</c> when any table is allowed. CMT writes none for owner and ignores it on
    /// import (each value's lookupentity decides); TXM021 flags values outside it, which CMT does not resolve.
    /// </summary>
    public string? LookupType { get; set; }

    /// <summary>Per-column override of <see cref="CmtDataSchema.DateMode"/> for datetime columns; one of <see cref="CmtDateModes"/> or <c>null</c>.</summary>
    public string? DateMode { get; set; }

    /// <summary>
    /// Attributes of this element the model does not know (TALXIS importer extensions such as <c>guidswap</c>, or anything newer), by XML
    /// name. They are kept so a package written from these objects, for example a merge, carries them; the writer adds, changes and removes them.
    /// </summary>
    public IDictionary<string, string> OtherAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    // A deep copy, so a merge that adds this field to another schema leaves the package it came from unchanged.
    internal CmtSchemaField Copy()
    {
        var copy = new CmtSchemaField
        {
            Name = Name,
            DisplayName = DisplayName,
            Type = Type,
            IsPrimaryKey = IsPrimaryKey,
            IsUpdateCompare = IsUpdateCompare,
            IsCustomField = IsCustomField,
            LookupType = LookupType,
            DateMode = DateMode,
            Source = Source
        };
        foreach (var other in OtherAttributes) copy.OtherAttributes[other.Key] = other.Value;
        return copy;
    }
}
