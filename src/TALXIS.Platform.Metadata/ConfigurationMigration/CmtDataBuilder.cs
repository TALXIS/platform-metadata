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
    /// <paramref name="warnings"/>. Many-to-many associations are joined by source record and relationship, their target ids united.
    /// Records repeated inside one package are merged the same way.
    /// </summary>
    /// <returns>
    /// The entity now in <paramref name="target"/>.
    /// </returns>
    public static CmtDataEntity MergeEntity(CmtData target, CmtDataEntity entity, ICollection<string>? warnings = null)
    {
        var existing = target.FindEntity(entity.Name) ?? target.AddEntity(entity.Name, entity.DisplayName);
        existing.DisplayName ??= entity.DisplayName;

        var conflicts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in entity.Records)
        {
            var current = existing.Records.FirstOrDefault(r => r.Id == record.Id);
            if (current is null)
            {
                existing.Records.Add(record);
                continue;
            }

            current.NewId ??= record.NewId;
            foreach (var field in record.Fields)
            {
                var currentField = current.Fields.FirstOrDefault(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal));
                if (currentField is null) current.Fields.Add(field);
                else if (currentField.Value != field.Value || currentField.LookupEntity != field.LookupEntity) conflicts[field.Name] = conflicts.TryGetValue(field.Name, out var count) ? count + 1 : 1;
            }
        }

        foreach (var conflict in conflicts)
        {
            warnings?.Add($"Entity '{entity.Name}': copies of the same record give field '{conflict.Key}' different values in {conflict.Value} record(s); the first copy's value is kept.");
        }

        foreach (var association in entity.ManyToManyRelationships)
        {
            var current = existing.ManyToManyRelationships.FirstOrDefault(m => m.SourceId == association.SourceId && string.Equals(m.RelationshipName, association.RelationshipName, StringComparison.Ordinal));
            if (current is null)
            {
                existing.ManyToManyRelationships.Add(association);
                continue;
            }

            foreach (var id in association.TargetIds.Where(id => !current.TargetIds.Contains(id)).ToList()) current.TargetIds.Add(id);
        }

        return existing;
    }
}
