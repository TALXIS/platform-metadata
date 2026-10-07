using TALXIS.Platform.Metadata.ConfigurationMigration.Data;

namespace TALXIS.Platform.Metadata.ConfigurationMigration.Building;

/// <summary>
/// Package-level edits on <see cref="CmtData"/> (data.xml), the record-side counterpart of <see cref="CmtSchemaBuilder"/>.
/// </summary>
public static class CmtDataBuilder
{
    /// <summary>
    /// Merges the records of <paramref name="entity"/> from another package into <paramref name="target"/>. A record that is already there
    /// (same id) gains the fields it lacks instead of being dropped, so the result does not depend on which package carries the fuller copy;
    /// when two packages give one field different values the first value is kept and the conflict is reported once per field in
    /// <paramref name="warnings"/>. Many-to-many associations are joined by source record and relationship, their target ids united;
    /// an association that names a different target table keeps the first package's targets and is reported.
    /// Records repeated inside one package are merged the same way.
    /// </summary>
    /// <returns>
    /// The entity now in <paramref name="target"/>.
    /// </returns>
    public static CmtDataEntity MergeEntity(CmtData target, CmtDataEntity entity, ICollection<string>? warnings = null)
    {
        var existing = target.FindEntity(entity.Name) ?? target.AddEntity(entity.Name, entity.DisplayName);
        existing.DisplayName ??= entity.DisplayName;
        AddMissingOtherAttributes(existing.OtherAttributes, entity.OtherAttributes);

        var conflicts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in entity.Records)
        {
            var current = existing.Records.FirstOrDefault(r => r.Id == record.Id);
            if (current == null)
            {
                existing.Records.Add(record);
                continue;
            }

            current.NewId ??= record.NewId;
            AddMissingOtherAttributes(current.OtherAttributes, record.OtherAttributes);
            foreach (var field in record.Fields)
            {
                var currentField = current.Fields.FirstOrDefault(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal));
                if (currentField == null)
                    current.Fields.Add(field);
                else if (!SameContent(currentField, field))
                    conflicts[field.Name] = conflicts.TryGetValue(field.Name, out var count) ? count + 1 : 1;
            }
        }

        foreach (var conflict in conflicts)
        {
            warnings?.Add($"Entity '{entity.Name}': copies of the same record give field '{conflict.Key}' different values in {conflict.Value} record(s); the first copy's value is kept.");
        }

        foreach (var association in entity.ManyToManyRelationships)
        {
            var current = existing.ManyToManyRelationships.FirstOrDefault(m => m.SourceId == association.SourceId && string.Equals(m.RelationshipName, association.RelationshipName, StringComparison.Ordinal));
            if (current == null)
            {
                existing.ManyToManyRelationships.Add(association);
                continue;
            }

            // Same record and relationship but another target table: the ids belong to different tables and must not be mixed.
            if (!string.Equals(current.TargetEntityName, association.TargetEntityName, StringComparison.Ordinal))
            {
                warnings?.Add($"Entity '{entity.Name}': record {association.SourceId} links relationship '{association.RelationshipName}' to '{current.TargetEntityName}' in one package and to '{association.TargetEntityName}' in another; only the first package's targets are kept.");
                continue;
            }

            foreach (var id in association.TargetIds.Where(id => !current.TargetIds.Contains(id)).ToList())
                current.TargetIds.Add(id);
        }

        return existing;
    }

    // Everything the importer reads from a field: the value, the lookup target and its fallback name, the file name, and for a
    // party list (whose value is empty) the attendee records, so two copies with different attendees are not taken as equal.
    private static bool SameContent(CmtDataField first, CmtDataField later) =>
        first.Value == later.Value
        && first.LookupEntity == later.LookupEntity
        && first.LookupEntityName == later.LookupEntityName
        && first.FileName == later.FileName
        && first.OtherAttributes.Count == later.OtherAttributes.Count
        && first.OtherAttributes.All(other => later.OtherAttributes.TryGetValue(other.Key, out var value) && value == other.Value)
        && SameAttendees(first.ActivityPointerRecords, later.ActivityPointerRecords);

    private static bool SameAttendees(IList<CmtDataRecord> first, IList<CmtDataRecord> later) =>
        first.Count == later.Count && first.Zip(later, SameAttendee).All(same => same);

    private static bool SameAttendee(CmtDataRecord first, CmtDataRecord later) =>
        first.Id == later.Id
        && first.Fields.Count == later.Fields.Count
        && first.Fields.Zip(later.Fields, (a, b) => a.Name == b.Name && SameContent(a, b)).All(same => same);

    // Extension attributes a later copy brings are added; one the first copy already has keeps its value, like the other attributes.
    private static void AddMissingOtherAttributes(IDictionary<string, string> first, IDictionary<string, string> later)
    {
        foreach (var other in later.Where(o => !first.ContainsKey(o.Key)).ToList()) first[other.Key] = other.Value;
    }
}
