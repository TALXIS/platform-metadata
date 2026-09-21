using System.Text.RegularExpressions;
using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Collapses xsi:nil tag pairs split across two lines in Solution.xml.
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
