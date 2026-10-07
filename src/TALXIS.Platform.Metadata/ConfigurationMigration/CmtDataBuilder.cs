namespace TALXIS.Platform.Metadata.ConfigurationMigration;

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
    /// Records repeated inside one package are first combined the way CMT imports them, copy after copy, so a later copy's value wins
    /// there without a warning. What is added is copied, so the packages being merged stay unchanged.
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
        var newIdConflicts = 0;
        foreach (var record in CombineRepeatedRecords(entity.Records))
        {
            var current = existing.Records.FirstOrDefault(r => r.Id == record.Id);
            if (current == null)
            {
                existing.Records.Add(record);
                continue;
            }

            // newId decides under which id the record is created, so two different ones change the import result with package order.
            if (current.NewId != null && record.NewId != null && current.NewId != record.NewId) newIdConflicts++;
            current.NewId ??= record.NewId;
            AddMissingOtherAttributes(current.OtherAttributes, record.OtherAttributes);
            foreach (var field in record.Fields)
            {
                var currentField = current.Fields.FirstOrDefault(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal));
                if (currentField == null)
                    current.Fields.Add(field);
                else if (!CmtDataComparison.SameContent(currentField, field))
                    conflicts[field.Name] = conflicts.TryGetValue(field.Name, out var count) ? count + 1 : 1;
            }
        }

        if (newIdConflicts > 0)
        {
            warnings?.Add($"Entity '{entity.Name}' has {newIdConflicts} record(s) whose copies give different newId values; "
                + "the first copy's newId is kept.");
        }

        foreach (var conflict in conflicts)
        {
            warnings?.Add($"Field '{entity.Name}.{conflict.Key}' has different values in copies of the same record ({conflict.Value} record(s)); "
                + "the first copy's value is kept.");
        }

        foreach (var association in entity.ManyToManyRelationships)
        {
            var current = existing.ManyToManyRelationships.FirstOrDefault(m => m.SourceId == association.SourceId && string.Equals(m.RelationshipName, association.RelationshipName, StringComparison.Ordinal));
            if (current == null)
            {
                existing.ManyToManyRelationships.Add(association.Copy());
                continue;
            }

            // Same record and relationship but another target table: the ids belong to different tables and must not be mixed.
            if (!string.Equals(current.TargetEntityName, association.TargetEntityName, StringComparison.Ordinal))
            {
                warnings?.Add($"Many-to-many relationship '{entity.Name}/{association.RelationshipName}' of record '{association.SourceId}' links "
                    + $"to '{current.TargetEntityName}' in one package and to '{association.TargetEntityName}' in another; only the first package's targets are kept.");
                continue;
            }

            foreach (var id in association.TargetIds.Where(id => !current.TargetIds.Contains(id)).ToList())
                current.TargetIds.Add(id);
        }

        return existing;
    }

    // CMT imports every copy of a record in order, so inside one package each copy updates what the earlier ones wrote: the last
    // value of a field wins and fields only an earlier copy sets stay. Returns copies, one per id, in first-seen order.
    private static List<CmtDataRecord> CombineRepeatedRecords(IEnumerable<CmtDataRecord> records)
    {
        var combined = new List<CmtDataRecord>();
        foreach (var record in records)
        {
            var current = combined.FirstOrDefault(r => r.Id == record.Id);
            if (current == null)
            {
                combined.Add(record.Copy());
                continue;
            }

            current.NewId = record.NewId ?? current.NewId;
            foreach (var other in record.OtherAttributes) current.OtherAttributes[other.Key] = other.Value;
            foreach (var field in record.Fields)
            {
                var earlier = current.Fields.FirstOrDefault(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal));
                if (earlier == null)
                    current.Fields.Add(field.Copy());
                else
                    current.Fields[current.Fields.IndexOf(earlier)] = field.Copy();
            }
        }

        return combined;
    }

    // Extension attributes a later copy brings are added; one the first copy already has keeps its value, like the other attributes.
    private static void AddMissingOtherAttributes(IDictionary<string, string> first, IDictionary<string, string> later)
    {
        foreach (var other in later.Where(o => !first.ContainsKey(o.Key)).ToList()) first[other.Key] = other.Value;
    }
}
