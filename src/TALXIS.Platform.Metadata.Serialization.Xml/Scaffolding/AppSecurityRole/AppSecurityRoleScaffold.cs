using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-app-security-role template post-action
/// scripts, with the tools-devkit-templates#152 semantics: role ids are accepted
/// bare, braced or quoted and rejected when not GUIDs, the AppModuleRoleMaps
/// replacement is built before the existing node is removed, and ids are emitted
/// in the braced form the solution XML uses everywhere else.
/// </summary>
public static class AppSecurityRoleScaffold
{
    public static ScaffoldResult Apply(AppSecurityRoleScaffoldRequest request)
    {
        var roleIds = ParseRoleIds(request.RoleIds);

        var doc = new XmlDocument();
        doc.Load(request.AppModuleFilePath);
        var appModule = doc.SelectSingleNode("//AppModule")
            ?? throw new InvalidOperationException($"AppModule root not found in '{request.AppModuleFilePath}'.");

        var roleMaps = doc.CreateElement("AppModuleRoleMaps");
        foreach (var roleId in roleIds)
        {
            var role = doc.CreateElement("Role");
            role.SetAttribute("id", roleId.ToString("B"));
            roleMaps.AppendChild(role);
        }

        // Drop the existing role map only now that the replacement exists in full,
        // so a failure above cannot leave the app module without any roles.
        if (appModule.SelectSingleNode("AppModuleRoleMaps") is XmlNode existing)
            appModule.RemoveChild(existing);
        appModule.AppendChild(roleMaps);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = new System.Text.UTF8Encoding(false),
        };
        using var writer = XmlWriter.Create(request.AppModuleFilePath, settings);
        doc.Save(writer);

        return new ScaffoldResult();
    }

    private static List<Guid> ParseRoleIds(string spec)
    {
        var ids = new List<Guid>();
        foreach (var raw in spec.Trim().Trim('[', ']').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = raw.Trim().Trim('"', '\'').Trim();
            if (candidate.Length == 0) continue;
            if (!Guid.TryParse(candidate, out var roleId))
            {
                throw new ArgumentException(
                    $"'{candidate}' is not a valid security role GUID. SecurityRolesIds expects a comma-separated list of role GUIDs.");
            }
            ids.Add(roleId);
        }

        if (ids.Count == 0)
            throw new ArgumentException($"SecurityRolesIds did not contain any security role GUIDs. Received: {spec}");

        return ids;
    }
}
