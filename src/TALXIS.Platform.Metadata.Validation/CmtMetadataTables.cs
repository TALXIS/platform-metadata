using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// The tables of a <see cref="Workspace"/> as the target environment sees them: every solution's copy of a table combined.
/// </summary>
/// <remarks>
/// <see cref="XmlWorkspaceReader.LoadMany"/> keeps only the copy of the solution loaded last, so a shell an app solution holds
/// would otherwise hide the columns the owning solution declares.
/// </remarks>
internal sealed class CmtMetadataTables
{
    private readonly Workspace _metadata;
    private readonly Dictionary<string, List<EntityMetadata>> _copies;
    private readonly Dictionary<string, EntityMetadata?> _combined = new(StringComparer.OrdinalIgnoreCase);

    public CmtMetadataTables(Workspace metadata)
    {
        _metadata = metadata;
        _copies = metadata.Layers.Stacks
            .Where(s => s.ComponentType == ComponentType.Entity)
            .SelectMany(s => s.Layers.Select(l => l.Metadata).OfType<EntityMetadata>())
            .GroupBy(e => e.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Distinct().ToList(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The table named <paramref name="logicalName"/>, combined from every copy, or <c>null</c> when the metadata does not have it.
    /// </summary>
    public EntityMetadata? Find(string logicalName)
    {
        if (!_combined.TryGetValue(logicalName, out var table))
        {
            table = Combine(logicalName);
            _combined[logicalName] = table;
        }

        return table;
    }

    private EntityMetadata? Combine(string logicalName)
    {
        var effective = _metadata.FindEntity(logicalName);
        if (effective == null) return null;
        if (!_copies.TryGetValue(logicalName, out var copies) || copies.Count < 2) return effective;

        // The copy that declares the primary id is the owner's full definition, so its columns and settings come first.
        var ordered = copies.OrderByDescending(c => !string.IsNullOrEmpty(c.PrimaryIdAttribute)).ToList();
        var owner = ordered[0];
        var combined = new EntityMetadata
        {
            LogicalName = effective.LogicalName,
            SchemaName = owner.SchemaName,
            DisplayName = owner.DisplayName,
            PrimaryIdAttribute = ordered.Select(c => c.PrimaryIdAttribute).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
            PrimaryNameAttribute = ordered.Select(c => c.PrimaryNameAttribute).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
            IsActivity = ordered.Any(c => c.IsActivity),
            IsImportable = ordered.Select(c => c.IsImportable).FirstOrDefault(i => i != null),
            Source = owner.Source
        };

        foreach (var attribute in ordered.SelectMany(c => c.Attributes).Where(a => combined.FindAttribute(a.LogicalName) == null).ToList())
            combined.AddAttribute(attribute);
        foreach (var relationship in ordered.SelectMany(c => c.Relationships).Distinct().ToList())
            combined.AddRelationship(relationship);

        return combined;
    }
}
