using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Accepts bare, braced or quoted role ids and rejects non-GUID values. Builds
/// the complete AppModuleRoleMaps node before removing the existing node,
/// and writes role ids in braced form.
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

        // Removes the existing role map only after the new node is complete,
        // so a preceding failure cannot leave the app module without roles.
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
