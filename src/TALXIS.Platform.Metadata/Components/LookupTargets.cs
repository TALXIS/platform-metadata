using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// The tables a lookup column can point to, whether the metadata lists them on the column or only through its relationships.
/// </summary>
public static class LookupTargets
{
    /// <summary>
    /// The lowercase logical names of the tables <paramref name="attribute"/> can point to: its own <see cref="LookupAttributeMetadata.Targets"/>
    /// when set, otherwise the referenced tables of the N:1 relationships backed by it. Empty when neither says.
    /// </summary>
    public static IReadOnlyList<string> Resolve(AttributeMetadata attribute, string entityLogicalName, IEnumerable<RelationshipMetadata> relationships)
    {
        // Unpacked solutions rarely list lookup targets on the column, so the relationships fill the gap.
        var own = attribute is LookupAttributeMetadata lookup ? Clean(lookup.Targets) : new List<string>();
        return own.Count > 0
            ? own
            : Clean(relationships.OfType<OneToManyRelationshipMetadata>()
                .Where(r => SameName(r.ReferencingEntity, entityLogicalName) && SameName(r.ReferencingAttribute, attribute.LogicalName))
                .Select(r => r.ReferencedEntity));
    }

    private static List<string> Clean(IEnumerable<string> targets) =>
        targets.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();

    private static bool SameName(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
