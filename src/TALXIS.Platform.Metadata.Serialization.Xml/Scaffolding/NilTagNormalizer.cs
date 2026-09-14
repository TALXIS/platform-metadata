using System.Text.RegularExpressions;
using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Collapses xsi:nil tag pairs the template engine splits across two lines in
/// Solution.xml; the NormalizeNilTags.ps1 shared by entity/attribute/solution templates.
/// </summary>
internal static class NilTagNormalizer
{
    public static void NormalizeSolutionXml(string solutionRootPath)
    {
        var solutionPath = Path.Combine(solutionRootPath, SolutionPackagerLayout.SolutionXmlPath);
        if (!File.Exists(solutionPath)) return;

        var content = File.ReadAllText(solutionPath);
        content = Regex.Replace(content, "(xsi:nil=\"true\")>\\s*\\r?\\n\\s*</", "$1></");
        File.WriteAllText(solutionPath, content);
    }
}
