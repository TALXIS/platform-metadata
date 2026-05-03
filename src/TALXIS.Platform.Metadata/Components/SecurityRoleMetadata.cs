namespace TALXIS.Platform.Metadata.Components;

public sealed class SecurityRoleMetadata : MetadataBase
{
    public required string RoleId { get; set; }
    public required string Name { get; set; }
    public bool IsInherited { get; set; }
    public string? IntroducedVersion { get; set; }

    private readonly List<RolePrivilegeMetadata> _privileges = new();
    public IReadOnlyList<RolePrivilegeMetadata> Privileges => _privileges;

    public void AddPrivilege(RolePrivilegeMetadata privilege) => _privileges.Add(privilege);
}

public sealed class RolePrivilegeMetadata
{
    public required string Name { get; set; }
    public required string Level { get; set; }
}
