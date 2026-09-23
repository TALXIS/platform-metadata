using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

internal static class WorkspaceFixtures
{
    public static void SeedProject(IWorkspaceContext context, string root, string uniqueName, string displayName, bool managed = false)
    {
        Write(context, Path.Combine(root, "Other", "Solution.xml"), SolutionXml(uniqueName, managed));
        Write(context, Path.Combine(root, "Entities", "test_entity", "Entity.xml"), EntityXml(displayName));
    }

    public static string SolutionXml(string uniqueName, bool managed = false) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml version="9.1">
          <SolutionManifest>
            <UniqueName>{uniqueName}</UniqueName>
            <Version>1.0</Version>
            <Managed>{(managed ? 1 : 0)}</Managed>
            <Publisher>
              <UniqueName>test</UniqueName>
              <CustomizationPrefix>test</CustomizationPrefix>
            </Publisher>
            <RootComponents>
              <RootComponent type="1" schemaName="test_entity" behavior="0" />
            </RootComponents>
          </SolutionManifest>
        </ImportExportXml>
        """;

    public static string EntityXml(string displayName) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Entity>
          <EntityInfo>
            <entity Name="test_entity">
              <EntitySetName>test_entities</EntitySetName>
              <LocalizedNames><LocalizedName description="{displayName}" languagecode="1033" /></LocalizedNames>
              <LocalizedCollectionNames><LocalizedCollectionName description="{displayName}s" languagecode="1033" /></LocalizedCollectionNames>
              <attributes />
            </entity>
          </EntityInfo>
        </Entity>
        """;

    public static string VirtualRoot(string name) => Path.Combine(Path.GetTempPath(), "virtual-workspace", name);

    public static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n");

    private static void Write(IWorkspaceContext context, string path, string content)
    {
        using var stream = context.Create(path);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(content);
    }
}
