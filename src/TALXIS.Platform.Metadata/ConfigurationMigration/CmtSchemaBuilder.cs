using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Package-level edits on a <see cref="CmtDataSchema"/>: build an entity from table metadata, add or refresh it, remove one
/// together with the relationships that point at it, and compute the import order from the lookups between declared entities.
/// </summary>
public static class CmtSchemaBuilder
{
    private const string OverriddenCreatedOn = "overriddencreatedon";
    private const string TransactionCurrencyId = "transactioncurrencyid";

    // Full selection adds the columns that let CMT carry the original creation date and audit users across.
    private static readonly HashSet<string> FullSelectionAuditColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        OverriddenCreatedOn, "createdby", "modifiedby"
    };

    /// <summary>
    /// Builds the data_schema.xml entry for one table from its metadata: the columns chosen by <see cref="CmtSchemaBuildOptions.FieldSelection"/>,
    /// each mapped with <see cref="CmtFieldTypeMapper"/>, the primary name (or id) as the updateCompare field, and relationship entries.
    /// Columns CMT cannot migrate (calculated, rollup, formula, unreadable, derived, virtual, <c>_base</c> money, and the columns
    /// in <see cref="DataverseSystemColumns.NotWritable"/>) are never included; bigint and file columns and types the mapper does not
    /// know are left out with a warning. N:1 entries are emitted only when <paramref name="target"/> declares the referenced table;
    /// M2M entries only with <see cref="CmtSchemaBuildOptions.IncludeManyToMany"/> and only on the relationship's Entity1 table,
    /// also when the other table is outside the package. Skipped relationships are reported in <paramref name="warnings"/>.
    /// The result is detached: add it with <see cref="AddOrReplaceEntity"/>.
    /// </summary>
    public static CmtSchemaEntity BuildEntity(
        EntityMetadata entity,
        IEnumerable<RelationshipMetadata> relationships,
        CmtSchemaBuildOptions options,
        ICollection<string> warnings,
        CmtDataSchema? target = null,
        Func<string, EntityMetadata?>? findEntity = null)
    {
        var result = new CmtSchemaEntity
        {
            Name = entity.LogicalName,
            DisplayName = entity.DisplayName.Default ?? entity.LogicalName,
            ObjectTypeCode = entity.ObjectTypeCode,
            PrimaryIdField = entity.PrimaryIdAttribute,
            PrimaryNameField = entity.PrimaryNameAttribute,
            DisablePlugins = false,
        };

        var relationshipList = relationships.ToList();
        AddFields(result, entity, relationshipList, options.FieldSelection, warnings);
        MarkUpdateCompare(result, entity);
        AddManyToOne(result, entity, relationshipList, warnings, target);
        if (options.IncludeManyToMany)
            AddManyToMany(result, entity, relationshipList, warnings, target, findEntity);

        return result;
    }

    /// <summary>
    /// Adds <paramref name="entity"/> to the schema, or refreshes the entity of the same name that is already there.
    /// When refreshing, fields already declared are kept as they are (hand edits such as updateCompare win) and only
    /// new fields are appended, unless <paramref name="replaceFields"/> is set. Relationships are always taken from
    /// <paramref name="entity"/>, and entity attributes it leaves <c>null</c> keep their current value.
    /// </summary>
    /// <returns>
    /// The entity now in the schema: <paramref name="entity"/> when it was added, otherwise the refreshed existing one.
    /// </returns>
    public static CmtSchemaEntity AddOrReplaceEntity(CmtDataSchema target, CmtSchemaEntity entity, bool replaceFields = false)
    {
        var existing = target.FindEntity(entity.Name);
        if (existing == null)
        {
            target.Entities.Add(entity);
            if (target.EntityImportOrder.Count > 0 && !target.EntityImportOrder.Contains(entity.Name))
                target.EntityImportOrder.Add(entity.Name);
            return entity;
        }

        // The entity taken from this schema and passed back: clearing its lists first would erase what is being copied in.
        if (ReferenceEquals(existing, entity)) return existing;

        existing.DisplayName = entity.DisplayName ?? existing.DisplayName;
        existing.ObjectTypeCode = entity.ObjectTypeCode ?? existing.ObjectTypeCode;
        existing.PrimaryIdField = entity.PrimaryIdField ?? existing.PrimaryIdField;
        existing.PrimaryNameField = entity.PrimaryNameField ?? existing.PrimaryNameField;
        existing.DisablePlugins = entity.DisablePlugins ?? existing.DisablePlugins;
        existing.SkipUpdate = entity.SkipUpdate ?? existing.SkipUpdate;
        existing.ForceCreate = entity.ForceCreate ?? existing.ForceCreate;
        existing.RenderLiquid = entity.RenderLiquid ?? existing.RenderLiquid;
        existing.FetchXmlFilter = entity.FetchXmlFilter ?? existing.FetchXmlFilter;

        if (replaceFields)
            existing.Fields.Clear();
        AddMissingFields(existing, entity);

        existing.Relationships.Clear();
        foreach (var relationship in entity.Relationships)
            existing.Relationships.Add(relationship);

        return existing;
    }

    /// <summary>
    /// Merges <paramref name="entity"/> from another package into the schema, the way package merging has always worked: the first package
    /// that declares the entity wins its attributes (later packages only fill attributes that are still absent), fields and relationships are
    /// joined by name with the first declaration winning, so the result does not depend on which package carries the relationships. An
    /// attribute two packages set to different values is reported in <paramref name="warnings"/>.
    /// </summary>
    /// <returns>
    /// The entity now in the schema.
    /// </returns>
    public static CmtSchemaEntity MergeEntity(CmtDataSchema target, CmtSchemaEntity entity, ICollection<string>? warnings = null)
    {
        var existing = target.FindEntity(entity.Name);
        if (existing == null) return AddOrReplaceEntity(target, entity);

        existing.DisplayName = KeepFirst(existing.Name, "displayname", existing.DisplayName, entity.DisplayName, warnings);
        existing.ObjectTypeCode = KeepFirst(existing.Name, "etc", existing.ObjectTypeCode, entity.ObjectTypeCode, warnings);
        existing.PrimaryIdField = KeepFirst(existing.Name, "primaryidfield", existing.PrimaryIdField, entity.PrimaryIdField, warnings);
        existing.PrimaryNameField = KeepFirst(existing.Name, "primarynamefield", existing.PrimaryNameField, entity.PrimaryNameField, warnings);
        existing.DisablePlugins = KeepFirst(existing.Name, "disableplugins", existing.DisablePlugins, entity.DisablePlugins, warnings);
        existing.SkipUpdate = KeepFirst(existing.Name, "skipupdate", existing.SkipUpdate, entity.SkipUpdate, warnings);
        existing.ForceCreate = KeepFirst(existing.Name, "forcecreate", existing.ForceCreate, entity.ForceCreate, warnings);
        existing.RenderLiquid = KeepFirst(existing.Name, "renderliquid", existing.RenderLiquid, entity.RenderLiquid, warnings);
        existing.FetchXmlFilter = KeepFirst(existing.Name, "filter", existing.FetchXmlFilter, entity.FetchXmlFilter, warnings);

        AddMissingFields(existing, entity);
        foreach (var relationship in entity.Relationships.Where(r => !existing.Relationships.Any(e => e.Name == r.Name)).ToList())
            existing.Relationships.Add(relationship);

        return existing;
    }

    /// <summary>
    /// Removes an entity, its import-order entry and every relationship on other entities that points at it
    /// (N:1 <c>referencedEntity</c> and M2M <c>m2mTargetEntity</c>). Lookup fields that target it stay: a lookup to a
    /// table outside the package is legal.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the schema does not declare the entity.
    /// </returns>
    public static bool RemoveEntity(CmtDataSchema target, string entityLogicalName)
    {
        var entity = target.FindEntity(entityLogicalName);
        if (entity == null) return false;

        target.Entities.Remove(entity);
        while (target.EntityImportOrder.Remove(entityLogicalName))
        {
        }

        foreach (var other in target.Entities)
        {
            var pointing = other.Relationships.Where(r => r.ReferencedEntity == entityLogicalName || r.M2mTargetEntity == entityLogicalName).ToList();
            foreach (var relationship in pointing)
                other.Relationships.Remove(relationship);
        }

        return true;
    }

    /// <summary>
    /// Orders the declared entities so that every entity comes after the entities it looks up (see <see cref="CmtSchemaEntity.ReferencedEntities"/>),
    /// writes the result to <see cref="CmtDataSchema.EntityImportOrder"/> and reorders <see cref="CmtDataSchema.Entities"/> to match.
    /// The starting point is the current order (the existing import order, then the element order). Entities listed in
    /// <paramref name="manualOrder"/> swap places only among themselves, taking the slots they already occupy, and keep exactly that
    /// relative order even when a lookup disagrees; the list may be partial. Every other entity stays where it is unless a lookup forces
    /// it to move, so nothing that was not listed jumps ahead of or behind its neighbours. Conflicts between the manual order and lookups,
    /// and lookup cycles, are reported in <paramref name="warnings"/>; self-references are ignored.
    /// </summary>
    public static void ResolveImportOrder(CmtDataSchema target, ICollection<string> warnings, IEnumerable<string>? manualOrder = null)
    {
        var manual = DeclaredManualOrder(target, manualOrder, warnings);
        var slots = PlaceManualOrder(CurrentOrder(target), manual);
        var parents = CollectParents(target, slots);
        LetManualOrderWin(parents, manual, warnings);

        var ordered = OrderAfterParents(slots, parents, manual, warnings);
        WriteOrder(target, ordered);
    }

    private static void AddFields(CmtSchemaEntity result, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships, CmtFieldSelection selection, ICollection<string> warnings)
    {
        var selected = entity.Attributes
            .Where(a => IsSelected(a, entity, selection))
            .OrderBy(a => a.LogicalName == entity.PrimaryIdAttribute ? 0 : 1)
            .ToList();

        foreach (var attribute in selected)
        {
            var type = MigratableType(attribute, entity, warnings);
            if (type == null) continue;

            result.Fields.Add(new CmtSchemaField
            {
                Name = attribute.LogicalName,
                DisplayName = attribute.DisplayName.Default ?? attribute.LogicalName,
                Type = type,
                IsPrimaryKey = attribute.LogicalName == entity.PrimaryIdAttribute,
                IsCustomField = attribute.IsCustomAttribute,
                LookupType = type == CmtFieldTypes.EntityReference ? LookupTargets(attribute, entity, relationships) : null,
            });
        }
    }

    private static bool IsSelected(AttributeMetadata attribute, EntityMetadata entity, CmtFieldSelection selection)
    {
        var name = attribute.LogicalName;
        if (name == entity.PrimaryIdAttribute || name == entity.PrimaryNameAttribute) return true;
        if (!CanMigrate(attribute)) return false;

        if (selection == CmtFieldSelection.Full)
            return (attribute.IsValidForCreate != false && attribute.IsValidForUpdate != false) || FullSelectionAuditColumns.Contains(name);

        // Money values import in the currency of this lookup; without it they land in the organisation's base currency.
        if (name == TransactionCurrencyId)
            return selection == CmtFieldSelection.Standard || entity.Attributes.Any(a => a.AttributeType == AttributeType.Money && IsSelected(a, entity, selection));

        if (DataverseSystemColumns.Contains(name))
            return selection == CmtFieldSelection.Standard && name == OverriddenCreatedOn;

        var minimal = attribute.IsCustomAttribute || attribute.RequiredLevel is RequiredLevel.ApplicationRequired or RequiredLevel.SystemRequired;
        if (minimal || selection == CmtFieldSelection.Minimal) return minimal;

        var nonOwnerLookup = attribute is LookupAttributeMetadata lookup && lookup.LookupKind != LookupKind.Owner;
        return nonOwnerLookup || attribute.AttributeType is AttributeType.Picklist or AttributeType.MultiSelectPicklist;
    }

    // Columns whose values CMT cannot carry whatever the selection: computed by the platform, derived from another column,
    // or never accepted on create or update. A flag the metadata source does not carry (null) counts as allowed.
    private static bool CanMigrate(AttributeMetadata attribute)
    {
        var name = attribute.LogicalName;
        if (attribute.IsValidForRead == false || DataverseSystemColumns.NotWritable.Contains(name)) return false;
        if (attribute.SourceType is AttributeSourceType.Calculated or AttributeSourceType.Rollup or AttributeSourceType.Formula) return false;
        if (attribute.AttributeOf != null && attribute.AttributeType is not (AttributeType.Image or AttributeType.MultiSelectPicklist)) return false;
        if (attribute.AttributeType == AttributeType.Money && name.EndsWith("_base", StringComparison.OrdinalIgnoreCase)) return false;

        return attribute.AttributeType != AttributeType.Virtual;
    }

    // CMT accepts bigint in a schema but drops the values on import, has no import conversion for the types the mapper does
    // not know, and refuses to export a schema with a file column (import reads files/<id>.bin, which export never writes).
    // All three are left out and reported instead of producing a column that silently migrates nothing or breaks the export.
    private static string? MigratableType(AttributeMetadata attribute, EntityMetadata entity, ICollection<string> warnings)
    {
        var type = CmtFieldTypeMapper.ToCmtType(attribute);
        if (type == CmtFieldTypes.BigInt)
        {
            warnings.Add($"Column '{entity.LogicalName}.{attribute.LogicalName}' is a bigint column. CMT accepts the type but drops its values on import, so the column is left out of the schema.");
            return null;
        }

        if (type == CmtFieldTypes.FileData)
        {
            warnings.Add($"Column '{entity.LogicalName}.{attribute.LogicalName}' is a file column. CMT export fails on a schema that declares it, so the column is left out; add it by hand to import file payloads from files/.");
            return null;
        }

        if (type == null)
            warnings.Add($"Column '{entity.LogicalName}.{attribute.LogicalName}' has type '{attribute.AttributeType}', which CMT cannot import, so the column is left out of the schema.");

        return type;
    }

    // Unpacked solutions rarely list lookup targets on the column, so the N:1 relationships fill the gap.
    private static string? LookupTargets(AttributeMetadata attribute, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships)
    {
        var targets = attribute is LookupAttributeMetadata lookup && lookup.Targets.Length > 0
            ? lookup.Targets
            : relationships.OfType<OneToManyRelationshipMetadata>()
                .Where(r => SameName(r.ReferencingEntity, entity.LogicalName) && SameName(r.ReferencingAttribute, attribute.LogicalName))
                .Select(r => r.ReferencedEntity)
                .ToArray();

        var distinct = targets.Select(Logical).Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 0 ? null : string.Join("|", distinct);
    }

    // Our policy, not CMT's (its generator's defaults are not recoverable): match on the primary name, else on the primary id.
    private static void MarkUpdateCompare(CmtSchemaEntity result, EntityMetadata entity)
    {
        var compare = result.FindField(entity.PrimaryNameAttribute ?? "") ?? result.FindField(entity.PrimaryIdAttribute ?? "");
        if (compare != null)
            compare.IsUpdateCompare = true;
    }

    private static void AddManyToOne(CmtSchemaEntity result, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships, ICollection<string> warnings, CmtDataSchema? target)
    {
        if (target == null) return;

        var lookups = relationships.OfType<OneToManyRelationshipMetadata>()
            .Where(r => SameName(r.ReferencingEntity, entity.LogicalName) && result.FindField(Logical(r.ReferencingAttribute)) != null);
        foreach (var relationship in lookups)
        {
            var referenced = Logical(relationship.ReferencedEntity);
            if (referenced != entity.LogicalName && target.FindEntity(referenced) == null)
            {
                warnings.Add($"'{entity.LogicalName}.{Logical(relationship.ReferencingAttribute)}' points to '{referenced}', which the package does not declare; the field stays, without a relationship entry.");
                continue;
            }

            result.Relationships.Add(new CmtSchemaRelationship
            {
                Name = relationship.SchemaName,
                ReferencingEntity = Logical(relationship.ReferencingEntity),
                ReferencingAttribute = Logical(relationship.ReferencingAttribute),
                ReferencedEntity = referenced,
                ReferencedAttribute = Logical(relationship.ReferencedAttribute),
            });
        }
    }

    // CMT's generator writes each many-to-many once, on the Entity1 table, named by the intersect entity, with Entity2 as the
    // target and Entity2's id (its intersect attribute) as the key, and no nested <fields>. Like CMT, the entry is kept even
    // when the other table lives in another package: its records must already exist.
    private static void AddManyToMany(CmtSchemaEntity result, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships, ICollection<string> warnings, CmtDataSchema? target, Func<string, EntityMetadata?>? findEntity)
    {
        var manyToMany = relationships.OfType<ManyToManyRelationshipMetadata>()
            .Where(r => SameName(r.Entity1LogicalName, entity.LogicalName));
        foreach (var relationship in manyToMany)
        {
            var other = Logical(relationship.Entity2LogicalName);
            var reflexive = other == entity.LogicalName;
            var otherEntity = reflexive ? result : target?.FindEntity(other);
            if (otherEntity == null)
                warnings.Add($"Many-to-many relationship '{relationship.SchemaName}' of '{entity.LogicalName}' targets '{other}', which the package does not declare; its records must already exist in the target environment.");

            var otherPrimaryKey = otherEntity?.PrimaryIdField ?? findEntity?.Invoke(other)?.PrimaryIdAttribute ?? other + "id";
            result.Relationships.Add(new CmtSchemaRelationship
            {
                Name = Logical(relationship.IntersectEntityName),
                IsManyToMany = true,
                IsReflexive = reflexive,
                RelatedEntityName = Logical(relationship.IntersectEntityName),
                M2mTargetEntity = other,
                M2mTargetEntityPrimaryKey = Logical(otherPrimaryKey),
            });
        }
    }

    // Relationship files carry schema names (Account, talxis_PriceListHeaderId); CMT and the model use lowercase logical names.
    private static string Logical(string name) => name.ToLowerInvariant();

    private static bool SameName(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void AddMissingFields(CmtSchemaEntity existing, CmtSchemaEntity entity)
    {
        foreach (var field in entity.Fields.Where(f => existing.FindField(f.Name) == null).ToList())
            existing.Fields.Add(field);
    }

    private static T? KeepFirst<T>(string entity, string attribute, T? first, T? next, ICollection<string>? warnings)
    {
        if (first == null) return next;

        if (next != null && !EqualityComparer<T>.Default.Equals(first, next))
            warnings?.Add($"Entity '{entity}': packages disagree on {attribute} ('{first}' and '{next}'); the first package's value is kept.");

        return first;
    }

    private static List<string> DeclaredManualOrder(CmtDataSchema target, IEnumerable<string>? manualOrder, ICollection<string> warnings)
    {
        var manual = new List<string>();
        foreach (var name in (manualOrder ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal))
        {
            if (target.FindEntity(name) == null)
                warnings.Add($"The manual import order names '{name}', which the data schema does not declare; it is ignored.");
            else
                manual.Add(name);
        }

        return manual;
    }

    // The existing import order first, then entities it does not list in element order.
    private static List<string> CurrentOrder(CmtDataSchema target)
    {
        return target.EntityImportOrder
            .Where(name => target.FindEntity(name) != null)
            .Concat(target.Entities.Select(e => e.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // Listed entities are refilled into the slots they already occupy, in the listed order; everything else keeps its slot.
    private static List<string> PlaceManualOrder(List<string> current, List<string> manual)
    {
        var listed = new HashSet<string>(manual, StringComparer.Ordinal);
        var nextListed = new Queue<string>(manual);
        return current.Select(name => listed.Contains(name) ? nextListed.Dequeue() : name).ToList();
    }

    private static Dictionary<string, HashSet<string>> CollectParents(CmtDataSchema target, List<string> slots)
    {
        var declared = new HashSet<string>(slots, StringComparer.Ordinal);
        var parents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in slots)
            parents[name] = new HashSet<string>(target.FindEntity(name)!.ReferencedEntities().Where(declared.Contains), StringComparer.Ordinal);

        return parents;
    }

    // A lookup to an entity listed later in the manual order is dropped (the manual order wins), and each listed entity
    // gets the one listed before it as an extra parent, so the listed order holds.
    private static void LetManualOrderWin(Dictionary<string, HashSet<string>> parents, List<string> manual, ICollection<string> warnings)
    {
        for (var i = 0; i < manual.Count; i++)
        {
            var child = manual[i];
            foreach (var parent in parents[child].Where(p => manual.IndexOf(p) > i).ToList())
            {
                parents[child].Remove(parent);
                warnings.Add($"'{child}' is imported before '{parent}', which it looks up, because the manual import order says so.");
            }

            if (i > 0)
                parents[child].Add(manual[i - 1]);
        }
    }

    // Always takes the earliest slot whose parents are already imported, so an entity moves only when a lookup forces it to.
    private static List<string> OrderAfterParents(List<string> slots, Dictionary<string, HashSet<string>> parents, List<string> manual, ICollection<string> warnings)
    {
        var ordered = new List<string>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var remaining = new List<string>(slots);
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(name => parents[name].All(emitted.Contains))
                ?? BreakCycle(remaining, parents, manual, emitted, warnings);

            remaining.Remove(next);
            emitted.Add(next);
            ordered.Add(next);
        }

        return ordered;
    }

    // Only lookup cycles block what is left. Break the one that waits on nothing outside itself (otherwise a child waiting on
    // a cycle would be taken for part of it), at its earliest slot that does not jump ahead of the manual order, and let CMT's
    // second pass fill in the lookups that point back into the cycle.
    private static string BreakCycle(List<string> remaining, Dictionary<string, HashSet<string>> parents, List<string> manual, HashSet<string> emitted, ICollection<string> warnings)
    {
        var ancestors = remaining.ToDictionary(name => name, name => PendingAncestors(name, parents, emitted), StringComparer.Ordinal);
        var cycle = remaining
            .Select(name => remaining.Where(other => other == name || (ancestors[name].Contains(other) && ancestors[other].Contains(name))).ToList())
            .First(members => members.Count > 1 && members.All(member => parents[member].All(p => emitted.Contains(p) || members.Contains(p))));

        var next = cycle.First(name =>
        {
            var rank = manual.IndexOf(name);
            return rank <= 0 || emitted.Contains(manual[rank - 1]);
        });

        foreach (var parent in parents[next].Where(p => !emitted.Contains(p)))
        {
            warnings.Add($"Entities '{next}' and '{parent}' look each other up (directly or through other entities); '{next}' is imported first and CMT's second pass fills in the lookup.");
        }

        return next;
    }

    // The not yet imported ancestors of an entity: its parents, their parents and so on, everything it waits on to be imported.
    private static HashSet<string> PendingAncestors(string entity, Dictionary<string, HashSet<string>> parents, HashSet<string> emitted)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(parents[entity].Where(p => !emitted.Contains(p)));
        while (stack.Count > 0)
        {
            var name = stack.Pop();
            if (!seen.Add(name)) continue;
            foreach (var parent in parents[name].Where(p => !emitted.Contains(p))) stack.Push(parent);
        }

        return seen;
    }

    private static void WriteOrder(CmtDataSchema target, List<string> ordered)
    {
        target.EntityImportOrder.Clear();
        foreach (var name in ordered)
            target.EntityImportOrder.Add(name);

        var entities = ordered.Select(name => target.FindEntity(name)!).ToList();
        target.Entities.Clear();
        foreach (var entity in entities)
            target.Entities.Add(entity);
    }
}
