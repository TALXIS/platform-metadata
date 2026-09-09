using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EntityViewScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string ViewId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-entity-view-scaffold").FullName;
    private readonly string _viewsDirectory;

    public EntityViewScaffoldTests()
    {
        _viewsDirectory = Path.Combine(_root, "Entities", EntityName, "SavedQueries");
        Directory.CreateDirectory(_viewsDirectory);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string ViewXml(string savedQueryId) => $"""
        <savedquery>
          <savedqueryid>{savedQueryId}</savedqueryid>
          <LocalizedNames>
            <LocalizedName description="Generated" languagecode="1033" />
          </LocalizedNames>
        </savedquery>
        """;

    [Fact]
    public void Apply_MissingSavedQueriesDirectory_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => EntityViewScaffold.Apply(new EntityViewScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = "udpp_missing",
        }));
    }

    [Fact]
    public void Apply_BracedViewFile_IsLeftUntouched()
    {
        var path = Path.Combine(_viewsDirectory, $"{{{ViewId}}}.xml");
        var content = ViewXml($"{{{ViewId}}}");
        File.WriteAllText(path, content);

        var result = EntityViewScaffold.Apply(new EntityViewScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
        });

        Assert.Empty(result.Warnings);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void Apply_UnbracedViewFile_GetsBracedFileNameAndSavedQueryId()
    {
        File.WriteAllText(Path.Combine(_viewsDirectory, $"{ViewId}.xml"), ViewXml(ViewId));

        EntityViewScaffold.Apply(new EntityViewScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
        });

        var bracedPath = Path.Combine(_viewsDirectory, $"{{{ViewId}}}.xml");
        Assert.True(File.Exists(bracedPath));
        Assert.False(File.Exists(Path.Combine(_viewsDirectory, $"{ViewId}.xml")));
        var doc = XDocument.Load(bracedPath);
        Assert.Equal($"{{{ViewId}}}", doc.Descendants("savedqueryid").Single().Value);
    }

    [Fact]
    public void Apply_ViaComponentScaffold_ResolvesEntityViewAlias()
    {
        File.WriteAllText(Path.Combine(_viewsDirectory, $"{ViewId}.xml"), ViewXml(ViewId));

        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "EntityView",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string> { ["entity"] = EntityName },
        });

        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(Path.Combine(_viewsDirectory, $"{{{ViewId}}}.xml")));
    }
}
