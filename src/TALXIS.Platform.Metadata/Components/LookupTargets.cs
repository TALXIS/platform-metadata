using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// The tables a lookup column can point to, shared by CMT schema generation and CMT metadata validation.
/// </summary>
public static class LookupTargets
{
    /// <summary>
    /// The lowercase logical names of the tables <paramref name="attribute"/> can point to: its own <see cref="LookupAttributeMetadata.Targets"/>
    /// when set, otherwise the referenced tables of the N:1 relationships backed by it. Empty when neither says.
    /// </summary>
    public static IReadOnlyList<string> Resolve(AttributeMetadata attribute, string entityLogicalName, IEnumerable<RelationshipMetadata> relationships)
    {
        // Unpacked solutions rarely list lookup targets on the column, and when they do it is by type code, so the relationships fill the gap.
        var targets = attribute is LookupAttributeMetadata lookup && lookup.Targets.Length > 0
            ? lookup.Targets
            : relationships.OfType<OneToManyRelationshipMetadata>()
                .Where(r => SameName(r.ReferencingEntity, entityLogicalName) && SameName(r.ReferencingAttribute, attribute.LogicalName))
                .Select(r => r.ReferencedEntity);

        return targets.Where(t => !string.IsNullOrEmpty(t)).Select(t => t.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool SameName(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
