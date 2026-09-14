namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="SecurityRoleScaffold"/>: the solution to patch and the
/// rendered role to register in its manifest.
/// </summary>
public sealed class SecurityRoleScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// GUID of the rendered security role, without braces.
    /// </summary>
    public string RoleId { get; set; } = "";
}
