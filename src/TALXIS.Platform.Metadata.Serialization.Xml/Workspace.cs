using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Container for all metadata loaded from a SolutionPackager workspace directory.
/// </summary>
public sealed class Workspace
{
    public string RootPath { get; }

    public Workspace(string rootPath)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
    }
    public Solution? Solution { get; set; }

    private readonly List<EntityMetadata> _entities = new();
    public IReadOnlyList<EntityMetadata> Entities => _entities;

    private readonly List<OptionSetMetadata> _globalOptionSets = new();
    public IReadOnlyList<OptionSetMetadata> GlobalOptionSets => _globalOptionSets;

    private readonly List<RelationshipMetadata> _relationships = new();
    public IReadOnlyList<RelationshipMetadata> Relationships => _relationships;

    /// <summary>
    /// Original XML documents stored by the reader for roundtrip-safe writing.
    /// Keys: "Solution.xml", "Entity:{logicalName}", "OptionSet:{name}", "Relationships.xml"
    /// </summary>
    internal Dictionary<string, XDocument> OriginalDocuments { get; } = new();

    public void AddEntity(EntityMetadata entity) => _entities.Add(entity);
    public void AddGlobalOptionSet(OptionSetMetadata optionSet) => _globalOptionSets.Add(optionSet);
    public void AddRelationship(RelationshipMetadata relationship) => _relationships.Add(relationship);

    public EntityMetadata? FindEntity(string logicalName) =>
        _entities.FirstOrDefault(e => string.Equals(e.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));
}
