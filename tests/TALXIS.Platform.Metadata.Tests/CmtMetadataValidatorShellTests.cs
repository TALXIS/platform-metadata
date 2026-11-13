using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtMetadataValidatorShellTests : IDisposable
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pm-shell-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TableOwnedByOneSolutionAndShelledByAnother_KeepsItsColumnsInEitherLoadOrder(bool ownerFirst)
    {
        var owner = Solution("Owner", """
            <attribute PhysicalName="tp_entityid">
              <Type>primarykey</Type>
              <Name>tp_entityid</Name>
              <LogicalName>tp_entityid</LogicalName>
            </attribute>
            <attribute PhysicalName="tp_name">
              <Type>nvarchar</Type>
              <Name>tp_name</Name>
              <LogicalName>tp_name</LogicalName>
              <DisplayMask>PrimaryName|ValidForAdvancedFind</DisplayMask>
            </attribute>
            """);
        var shell = Solution("App", string.Empty);
        var sources = ownerFirst ? new[] { owner, shell } : new[] { shell, owner };
        var metadata = new XmlWorkspaceReader().LoadMany(sources.Select((path, order) => new SolutionWorkspaceSource(path, order)));
        var schema = new CmtDataSchema();
        schema.AddEntity("test_entity", "tp_entityid", "tp_name").AddField("tp_name", CmtFieldTypes.String, updateCompare: true);

        var findings = new CmtMetadataValidator().Validate(new CmtPackage(schema), metadata);

        Assert.Empty(findings);
    }

    private string Solution(string uniqueName, string attributes)
    {
        var path = Path.Combine(_root, uniqueName);
        foreach (var directory in Directory.GetDirectories(SamplePath, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(directory.Replace(SamplePath, path));
        foreach (var file in Directory.GetFiles(SamplePath, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(SamplePath, path));

        var solutionPath = Path.Combine(path, "Other", "Solution.xml");
        File.WriteAllText(solutionPath, File.ReadAllText(solutionPath).Replace("<UniqueName>TestSolution</UniqueName>", $"<UniqueName>{uniqueName}</UniqueName>"));

        var entityPath = Path.Combine(path, "Entities", "test_entity", "Entity.xml");
        var entity = File.ReadAllText(entityPath);
        var start = entity.IndexOf("<attributes>", StringComparison.Ordinal);
        var end = entity.IndexOf("</attributes>", StringComparison.Ordinal) + "</attributes>".Length;
        File.WriteAllText(entityPath, entity.Substring(0, start) + $"<attributes>{attributes}</attributes>" + entity.Substring(end));
        return path;
    }
}
