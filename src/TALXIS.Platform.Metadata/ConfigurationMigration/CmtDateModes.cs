namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Values of the CMT <c>dateMode</c> attribute (root and per field). CMT deserialises them as an enum, so any
/// other spelling makes it reject the whole schema.
/// </summary>
public static class CmtDateModes
{
    /// <summary>Import datetimes as exported.</summary>
    public const string Absolute = "absolute";

    /// <summary>Shift datetimes by the offset between export and import time.</summary>
    public const string Relative = "relative";

    /// <summary>Shift datetimes by whole days only.</summary>
    public const string RelativeDaily = "relativeDaily";

    /// <summary>All valid values.</summary>
    public static readonly IReadOnlyCollection<string> All = new HashSet<string>(StringComparer.Ordinal) { Absolute, Relative, RelativeDaily };
}
