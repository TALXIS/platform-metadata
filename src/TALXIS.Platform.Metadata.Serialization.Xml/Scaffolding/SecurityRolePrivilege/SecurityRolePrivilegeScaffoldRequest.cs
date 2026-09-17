namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="SecurityRolePrivilegeScaffold"/>: the role file to
/// patch and the privilege spec to apply to it.
/// </summary>
public sealed class SecurityRolePrivilegeScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the role XML file (Roles/&lt;role name&gt;.xml).
    /// </summary>
    public string RoleFilePath { get; set; } = "";

    /// <summary>
    /// Logical name of the entity the privileges target (with publisher prefix).
    /// </summary>
    public string EntityLogicalName { get; set; } = "";

    /// <summary>
    /// Privilege spec: a JSON array of {privilegetype, level} objects; bare
    /// (unquoted) keys and values and the "type" key alias are accepted.
    /// </summary>
    public string Privileges { get; set; } = "";
}
