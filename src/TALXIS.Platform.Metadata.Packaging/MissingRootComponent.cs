using System.Text.RegularExpressions;

namespace TALXIS.Platform.Metadata.Packaging;

/// <summary>
/// A root component that the packager reported as not defined in customizations.
/// </summary>
/// <param name="Type">The component type as the packager prints it: a type name such as <c>SystemForm</c>, or a number for types it does not know.</param>
/// <param name="Key">The packager's key for the component: a braced id, or <c>{Type}-{schema name}</c> for components without one.</param>
public sealed record MissingRootComponent(string Type, string Key)
{
    // One row of SolutionPackagerLib's PrintComponentInfos output. The key may contain quotes (schema names are not validated).
    private static readonly Regex RowPattern =
        new(@"^Type='(?<type>[^']+)', Id \(or schema name\)='(?<key>.*)'\.$", RegexOptions.Compiled);

    // The first line of the warning is the header. Returns false if there are no rows or any row is not in the packager's format.
    internal static bool TryParseWarning(string warning, out IReadOnlyList<MissingRootComponent> components)
    {
        var matches = warning.Split('\n')
            .Skip(1)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => RowPattern.Match(line))
            .ToArray();

        if (matches.Length == 0 || matches.Any(match => !match.Success))
        {
            components = [];
            return false;
        }

        components = matches
            .Select(match => new MissingRootComponent(match.Groups["type"].Value, match.Groups["key"].Value))
            .ToArray();
        return true;
    }

    internal static string FormatWarning(IEnumerable<MissingRootComponent> components)
    {
        return SolutionPackagerResult.MissingRootComponentsWarningPrefix + ":" + Environment.NewLine
            + string.Join(Environment.NewLine, components.Select(component => $"  Type='{component.Type}', Id (or schema name)='{component.Key}'."));
    }
}
