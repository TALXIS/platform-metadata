using System.Globalization;
using TALXIS.Platform.Metadata.DataMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// The value encodings Microsoft CMT reads per schema field type; it drops, zeroes or misreads anything else.
/// Types without a fixed text form (string, lookups, files, images) always pass.
/// </summary>
internal static class CmtValueFormats
{
    private const string BracketPrefix = "[-1,";
    private const string BracketSuffix = ",-1]";

    /// <summary>Whether CMT reads <paramref name="value"/> as a value of the CMT field <paramref name="type"/>.</summary>
    public static bool IsValid(string type, string value) => type switch
    {
        // XmlSerializer would also take 1/0, but CMT's data import reads them (and yes, t, f) as false.
        CmtFieldTypes.Bool => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase),
        CmtFieldTypes.Number or CmtFieldTypes.OptionSetValue => IsInteger(value),
        // No currency symbols and no comma decimals: CMT strips or misreads both.
        CmtFieldTypes.Decimal or CmtFieldTypes.Money => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        CmtFieldTypes.Float => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        CmtFieldTypes.DateTime => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        CmtFieldTypes.Guid => Guid.TryParse(value, out _),
        CmtFieldTypes.OptionSetValueCollection => IsOptionSetValueCollection(value),
        _ => true
    };

    /// <summary>Whether a numeric <paramref name="value"/> is only readable with a thousands separator, as in <c>1,234</c>.</summary>
    public static bool HasThousandsSeparator(string type, string value)
    {
        if (type != CmtFieldTypes.Number && type != CmtFieldTypes.Decimal && type != CmtFieldTypes.Money && type != CmtFieldTypes.Float) return false;
        return value.IndexOf(',') >= 0 && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    // Comma-separated integers, or the form CMT exports: the same list between -1 sentinels, [-1,a,b,-1].
    private static bool IsOptionSetValueCollection(string value)
    {
        if (value == "[-1,-1]") return true;
        var list = value.StartsWith(BracketPrefix, StringComparison.Ordinal) && value.EndsWith(BracketSuffix, StringComparison.Ordinal)
            ? value.Substring(BracketPrefix.Length, value.Length - BracketPrefix.Length - BracketSuffix.Length)
            : value;
        return list.Split(',').All(IsInteger);
    }

    private static bool IsInteger(string value) => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
}
