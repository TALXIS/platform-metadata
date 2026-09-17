using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EnvironmentVariableValueScaffoldTests : IDisposable
{
    private const string ValueId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000381";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-envvar-value-scaffold").FullName;
    private readonly string _valuesPath;

    public EnvironmentVariableValueScaffoldTests()
    {
        var dir = Path.Combine(_root, "environmentvariabledefinitions", "udpp_apiurl");
        Directory.CreateDirectory(dir);
        _valuesPath = Path.Combine(dir, "environmentvariablevalues.json");
        File.WriteAllText(_valuesPath, "{}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_WritesPayloadVerbatim()
    {
        EnvironmentVariableValueScaffold.Apply(new EnvironmentVariableValueScaffoldRequest
        {
            SolutionRootPath = _root,
            ValuesFilePath = _valuesPath,
            Value = "https://example.test/api?x=1&y=\"q\"",
            ValueId = ValueId,
        });

        var text = File.ReadAllText(_valuesPath);
        Assert.Contains($"\"@environmentvariablevalueid\": \"{ValueId}\"", text);
        Assert.Contains("\"iscustomizable\": \"1\"", text);
        Assert.Contains("\"value\": \"https://example.test/api?x=1&y=\\\"q\\\"\"", text);
        Assert.EndsWith("}\n", text);
    }

    [Fact]
    public void Apply_MissingStub_Throws()
    {
        File.Delete(_valuesPath);

        Assert.Throws<FileNotFoundException>(() => EnvironmentVariableValueScaffold.Apply(new EnvironmentVariableValueScaffoldRequest
        {
            SolutionRootPath = _root,
            ValuesFilePath = _valuesPath,
            Value = "x",
            ValueId = ValueId,
        }));
    }
}
