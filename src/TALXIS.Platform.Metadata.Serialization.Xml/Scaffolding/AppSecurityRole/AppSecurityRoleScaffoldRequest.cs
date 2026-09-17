namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="AppSecurityRoleScaffold"/>: the app module to patch
/// and the security roles to grant access to it.
/// </summary>
public sealed class AppSecurityRoleScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the app's AppModule.xml.
    /// </summary>
    public string AppModuleFilePath { get; set; } = "";

    /// <summary>
    /// Comma-separated list of role GUIDs; braces and quotes around entries are accepted.
    /// </summary>
    public string RoleIds { get; set; } = "";
}
