using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionRootLocatorTests : IDisposable
{
    private readonly string _base = Directory.CreateTempSubdirectory("metadata-solution-root-locator").FullName;

    public void Dispose() => Directory.Delete(_base, recursive: true);

    private void CreateSolutionRoot(params string[] segments)
    {
        var other = Path.Combine(Path.Combine(new[] { _base }.Concat(segments).ToArray()), "Other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "Solution.xml"), "<ImportExportXml />");
    }

    [Fact]
    public void Resolve_ValidRequestedRoot_IsHonored()
    {
        CreateSolutionRoot("Declarations");

        var resolved = SolutionRootLocator.Resolve("Declarations", _base);

        Assert.Equal("Declarations", resolved);
    }

    [Fact]
    public void Resolve_PlaceholderRequested_DetectsBaseDirectoryRoot()
    {
        CreateSolutionRoot();

        var resolved = SolutionRootLocator.Resolve("__solution-root-path__", _base);

        Assert.Equal(_base, resolved);
    }

    [Fact]
    public void Resolve_ReadsCsprojProperty()
    {
        CreateSolutionRoot("Declarations", "Source");
        Directory.CreateDirectory(Path.Combine(_base, "Declarations", "Decoy"));
        File.WriteAllText(Path.Combine(_base, "Sandbox.csproj"), """
            <Project Sdk="TALXIS.DevKit.Sdk.Dataverse"><PropertyGroup><SolutionRootPath>Declarations/Source</SolutionRootPath></PropertyGroup></Project>
            """);

        var resolved = SolutionRootLocator.Resolve(null, _base);

        Assert.Equal(Path.Combine(_base, "Declarations/Source"), resolved);
    }

    [Fact]
    public void Resolve_FallsBackToUniqueNestedRoot()
    {
        CreateSolutionRoot("Declarations");

        var resolved = SolutionRootLocator.Resolve(null, _base);

        Assert.Equal(Path.Combine(_base, "Declarations"), resolved);
    }

    [Fact]
    public void Resolve_MultipleNestedRoots_Throws()
    {
        CreateSolutionRoot("A");
        CreateSolutionRoot("B");

        Assert.Throws<InvalidOperationException>(() => SolutionRootLocator.Resolve(null, _base));
    }

    [Fact]
    public void Resolve_NothingFound_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SolutionRootLocator.Resolve(null, _base));
    }
}
