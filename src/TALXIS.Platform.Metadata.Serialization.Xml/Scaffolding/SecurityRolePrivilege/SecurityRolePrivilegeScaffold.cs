using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Parses privilege specifications as quoted or bare JSON, accepting "type" as
/// an alias for the privilege type key. Maps UI levels to role XSD values
/// (User=Basic, BusinessUnit=Local, ParentChild=Deep); None omits the privilege.
/// Unknown types or levels fail with the valid values listed. Merges privileges
/// into the role file idempotently, sorted alphabetically by name.
/// </summary>
public static class SecurityRolePrivilegeScaffold
{
    private static readonly Dictionary<string, string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Create"] = "Create",
        ["Read"] = "Read",
        ["Write"] = "Write",
        ["Delete"] = "Delete",
        ["Append"] = "Append",
        ["AppendTo"] = "AppendTo",
        ["Assign"] = "Assign",
        ["Share"] = "Share",
    };

    private static readonly Dictionary<string, string> KnownLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = "None",
        ["Basic"] = "Basic",
        ["User"] = "Basic",
        ["Local"] = "Local",
        ["BusinessUnit"] = "Local",
        ["Deep"] = "Deep",
        ["ParentChild"] = "Deep",
        ["Global"] = "Global",
    };

    public static ScaffoldResult Apply(SecurityRolePrivilegeScaffoldRequest request)
    {
        var privileges = ParsePrivileges(request.Privileges, request.EntityLogicalName);

        var roleDoc = ScaffoldXmlFile.Load(request.RoleFilePath);
        var rolePrivilegesNode = roleDoc.SelectSingleNode("//Role/RolePrivileges")
            ?? throw new InvalidOperationException($"Could not find Role/RolePrivileges node in '{request.RoleFilePath}'.");

        foreach (var (name, level) in privileges)
        {
            if (rolePrivilegesNode.SelectSingleNode($"RolePrivilege[@name='{name}']") is XmlElement existing)
            {
                existing.SetAttribute("level", level);
                continue;
            }

            var privilege = roleDoc.CreateElement("RolePrivilege");
            privilege.SetAttribute("name", name);
            privilege.SetAttribute("level", level);

            XmlNode? insertBefore = null;
            foreach (XmlElement sibling in rolePrivilegesNode.SelectNodes("RolePrivilege")!)
            {
                if (string.Compare(sibling.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    insertBefore = sibling;
                    break;
                }
            }

            if (insertBefore != null) rolePrivilegesNode.InsertBefore(privilege, insertBefore);
            else rolePrivilegesNode.AppendChild(privilege);
        }

        ScaffoldXmlFile.Save(roleDoc, request.RoleFilePath);
        return new ScaffoldResult();
    }

    private static List<(string Name, string Level)> ParsePrivileges(string spec, string entityLogicalName)
    {
        // Bare keys/values (the terser documented form) get quoted; valid JSON is untouched.
        var normalized = Regex.Replace(spec, @"(\w+):\s*(\w+)", "\"$1\": \"$2\"");

        List<Dictionary<string, string>>? permissions;
        try
        {
            permissions = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(normalized);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"PrivilegeTypeAndLevel is not valid JSON: {ex.Message}. Received: {spec}");
        }

        if (permissions == null || permissions.Count == 0)
            throw new ArgumentException($"PrivilegeTypeAndLevel did not contain any privileges. Received: {spec}");

        var result = new List<(string, string)>();
        for (var i = 0; i < permissions.Count; i++)
        {
            var rawType = Lookup(permissions[i], "privilegetype", "type");
            var rawLevel = Lookup(permissions[i], "level");

            if (string.IsNullOrWhiteSpace(rawType))
                throw new ArgumentException($"Privilege at position {i + 1} has no 'privilegetype'. Expected one of: {string.Join(", ", KnownTypes.Values)}.");
            if (string.IsNullOrWhiteSpace(rawLevel))
                throw new ArgumentException($"Privilege '{rawType}' has no 'level'. Expected one of: None, Basic, Local, Deep, Global.");
            if (!KnownTypes.TryGetValue(rawType, out var privilegeType))
                throw new ArgumentException($"Unknown privilege type '{rawType}'. Expected one of: {string.Join(", ", KnownTypes.Values)}.");
            if (!KnownLevels.TryGetValue(rawLevel, out var level))
                throw new ArgumentException($"Unknown privilege level '{rawLevel}'. Expected one of: None, Basic, Local, Deep, Global.");

            // Absence of the privilege is how "no access" is expressed.
            if (level == "None") continue;

            result.Add(($"prv{privilegeType}{entityLogicalName}", level));
        }

        return result;
    }

    private static string? Lookup(Dictionary<string, string> permission, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var pair in permission)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            }
        }
        return null;
    }
}
