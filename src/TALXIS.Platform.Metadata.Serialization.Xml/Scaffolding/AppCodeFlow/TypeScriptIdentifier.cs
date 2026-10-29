using System.Text.RegularExpressions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Turns flow and property names into TypeScript identifiers the way the pa CLI codegen does.
/// </summary>
internal static class TypeScriptIdentifier
{
    public static string From(string value, bool stripWhitespace)
    {
        var result = value.Trim();
        if (stripWhitespace) result = Regex.Replace(result, @"\s", "");
        result = Regex.Replace(result, @"[^a-zA-Z0-9_$]", "_");
        if (result.Length > 0 && char.IsDigit(result[0])) result = "_" + result;
        return result;
    }
}
