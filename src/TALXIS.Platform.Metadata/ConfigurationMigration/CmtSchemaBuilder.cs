using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Package-level edits on a <see cref="CmtDataSchema"/>: build an entity from table metadata, add or refresh it, remove one
/// together with the relationships that point at it, and compute the import order from the lookups between declared entities.
/// </summary>
public static class CmtSchemaBuilder
{
    private static readonly HashSet<string> LookupFieldTypes = new(StringComparer.Ordinal)
    {
        CmtFieldTypes.EntityReference, CmtFieldTypes.Customer
    };

    private static readonly HashSet<string> NeverMigrated = new(StringComparer.OrdinalIgnoreCase)
    {
        "versionnumber", "createdon", "modifiedon", "owneridname", "owneridtype", "owneridyominame"
    };

    private static readonly HashSet<string> FullExtras = new(StringComparer.OrdinalIgnoreCase)
    {
        "overriddencreatedon", "createdby", "modifiedby"
    };

    /// <summary>
    /// Builds the data_schema.xml entry for one table from its metadata: the columns chosen by <see cref="CmtSchemaBuildOptions.FieldSelection"/>,
    /// each mapped with <see cref="CmtFieldTypeMapper"/>, the primary name (or id) as the updateCompare field, and relationship entries.
    /// Columns CMT cannot migrate (calculated, rollup, formula, unreadable, derived, virtual, <c>_base</c> money, versionnumber, created/modified on)
    /// are never included; bigint columns and types the mapper does not know are left out with a warning. N:1 entries are emitted only when <paramref name="target"/> declares the referenced table, M2M entries
    /// only with <see cref="CmtSchemaBuildOptions.IncludeManyToMany"/>, also when the other table is outside the package; skipped relationships are reported in <paramref name="warnings"/>.
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
        var selected = entity.Attributes
            .Where(a => IsSelected(a, entity, options.FieldSelection))
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
                LookupType = type == CmtFieldTypes.EntityReference ? LookupTargets(attribute, entity, relationshipList) : null,
            });
        }

        var compare = result.FindField(entity.PrimaryNameAttribute ?? "") ?? result.FindField(entity.PrimaryIdAttribute ?? "");
        if (compare is not null) compare.IsUpdateCompare = true;

        AddManyToOne(result, entity, relationshipList, warnings, target);
        if (options.IncludeManyToMany) AddManyToMany(result, entity, relationshipList, warnings, target, findEntity);
        return result;
    }

    // CMT accepts bigint in a schema but drops the values on import, and has no import conversion for the types the mapper
    // does not know, so both are left out and reported instead of producing a column that silently migrates nothing.
    private static string? MigratableType(AttributeMetadata attribute, EntityMetadata entity, ICollection<string> warnings)
    {
        var type = CmtFieldTypeMapper.ToCmtType(attribute);
        if (type == CmtFieldTypes.BigInt)
        {
            warnings.Add($"Column '{entity.LogicalName}.{attribute.LogicalName}' is a bigint column. CMT accepts the type but drops its values on import, so the column is left out of the schema.");
            return null;
        }

        if (type == null)
        {
            warnings.Add($"Column '{entity.LogicalName}.{attribute.LogicalName}' has type '{attribute.AttributeType}', which CMT cannot import, so the column is left out of the schema.");
        }

        return type;
    }

    private static bool IsSelected(AttributeMetadata attribute, EntityMetadata entity, CmtFieldSelection selection)
    {
        var name = attribute.LogicalName;
        if (name == entity.PrimaryIdAttribute || name == entity.PrimaryNameAttribute) return true;
        if (attribute.IsValidForRead == false || NeverMigrated.Contains(name)) return false;
        if (attribute.SourceType is AttributeSourceType.Calculated or AttributeSourceType.Rollup or AttributeSourceType.Formula) return false;
        if (attribute.AttributeOf is not null && attribute.AttributeType is not (AttributeType.Image or AttributeType.MultiSelectPicklist)) return false;
        if (attribute.AttributeType == AttributeType.Money && name.EndsWith("_base", StringComparison.OrdinalIgnoreCase)) return false;
        if (attribute.AttributeType == AttributeType.Virtual) return false;

        if (selection == CmtFieldSelection.Full)
        {
            return (attribute.IsValidForCreate != false && attribute.IsValidForUpdate != false) || FullExtras.Contains(name);
        }

        if (DataverseSystemColumns.Contains(name)) return selection == CmtFieldSelection.Standard && name == "overriddencreatedon";

        var isMinimal = attribute.IsCustomAttribute || attribute.RequiredLevel is RequiredLevel.ApplicationRequired or RequiredLevel.SystemRequired;
        if (selection == CmtFieldSelection.Minimal || isMinimal) return isMinimal;

        return attribute is LookupAttributeMetadata { LookupKind: not LookupKind.Owner }
            || attribute.AttributeType is AttributeType.Picklist or AttributeType.MultiSelectPicklist;
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

    // Relationship files carry schema names (Account, talxis_PriceListHeaderId); CMT and the model use lowercase logical names.
    private static string Logical(string name) => name.ToLowerInvariant();

    private static bool SameName(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void AddManyToOne(CmtSchemaEntity result, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships, ICollection<string> warnings, CmtDataSchema? target)
    {
        if (target is null) return;

        var lookups = relationships.OfType<OneToManyRelationshipMetadata>()
            .Where(r => SameName(r.ReferencingEntity, entity.LogicalName) && result.FindField(Logical(r.ReferencingAttribute)) is not null);
        foreach (var relationship in lookups)
        {
            var referenced = Logical(relationship.ReferencedEntity);
            if (referenced != entity.LogicalName && target.FindEntity(referenced) is null)
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

    // Like CMT, a many-to-many entry is kept even when the other table lives in another package: its records must already exist.
    private static void AddManyToMany(CmtSchemaEntity result, EntityMetadata entity, IReadOnlyList<RelationshipMetadata> relationships, ICollection<string> warnings, CmtDataSchema? target, Func<string, EntityMetadata?>? findEntity)
    {
        var manyToMany = relationships.OfType<ManyToManyRelationshipMetadata>()
            .Where(r => SameName(r.Entity1LogicalName, entity.LogicalName) || SameName(r.Entity2LogicalName, entity.LogicalName));
        foreach (var relationship in manyToMany)
        {
            var other = Logical(SameName(relationship.Entity1LogicalName, entity.LogicalName) ? relationship.Entity2LogicalName : relationship.Entity1LogicalName);
            var isReflexive = other == entity.LogicalName;
            var otherEntity = isReflexive ? result : target?.FindEntity(other);
            if (otherEntity is null)
            {
                warnings.Add($"Many-to-many relationship '{relationship.SchemaName}' of '{entity.LogicalName}' targets '{other}', which the package does not declare; its records must already exist in the target environment.");
            }

            var otherPrimaryKey = otherEntity?.PrimaryIdField ?? findEntity?.Invoke(other)?.PrimaryIdAttribute ?? other + "id";

            result.Relationships.Add(new CmtSchemaRelationship
            {
                Name = Logical(relationship.IntersectEntityName),
                IsManyToMany = true,
                IsReflexive = isReflexive,
                RelatedEntityName = Logical(relationship.IntersectEntityName),
                M2mTargetEntity = other,
                M2mTargetEntityPrimaryKey = Logical(otherPrimaryKey),
            });
        }
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
        if (existing is null)
        {
            target.Entities.Add(entity);
            if (target.EntityImportOrder.Count > 0 && !target.EntityImportOrder.Contains(entity.Name)) target.EntityImportOrder.Add(entity.Name);
            return entity;
        }

        existing.DisplayName = entity.DisplayName ?? existing.DisplayName;
        existing.ObjectTypeCode = entity.ObjectTypeCode ?? existing.ObjectTypeCode;
        existing.PrimaryIdField = entity.PrimaryIdField ?? existing.PrimaryIdField;
        existing.PrimaryNameField = entity.PrimaryNameField ?? existing.PrimaryNameField;
        existing.DisablePlugins = entity.DisablePlugins ?? existing.DisablePlugins;
        existing.SkipUpdate = entity.SkipUpdate ?? existing.SkipUpdate;
        existing.ForceCreate = entity.ForceCreate ?? existing.ForceCreate;
        existing.RenderLiquid = entity.RenderLiquid ?? existing.RenderLiquid;
        existing.FetchXmlFilter = entity.FetchXmlFilter ?? existing.FetchXmlFilter;

        if (replaceFields) existing.Fields.Clear();
        foreach (var field in entity.Fields.Where(f => existing.FindField(f.Name) is null).ToList()) existing.Fields.Add(field);

        existing.Relationships.Clear();
        foreach (var relationship in entity.Relationships) existing.Relationships.Add(relationship);
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
        if (existing is null) return AddOrReplaceEntity(target, entity);

        existing.DisplayName = KeepFirst(existing.Name, "displayname", existing.DisplayName, entity.DisplayName, warnings);
        existing.ObjectTypeCode = KeepFirst(existing.Name, "etc", existing.ObjectTypeCode, entity.ObjectTypeCode, warnings);
        existing.PrimaryIdField = KeepFirst(existing.Name, "primaryidfield", existing.PrimaryIdField, entity.PrimaryIdField, warnings);
        existing.PrimaryNameField = KeepFirst(existing.Name, "primarynamefield", existing.PrimaryNameField, entity.PrimaryNameField, warnings);
        existing.DisablePlugins = KeepFirst(existing.Name, "disableplugins", existing.DisablePlugins, entity.DisablePlugins, warnings);
        existing.SkipUpdate = KeepFirst(existing.Name, "skipupdate", existing.SkipUpdate, entity.SkipUpdate, warnings);
        existing.ForceCreate = KeepFirst(existing.Name, "forcecreate", existing.ForceCreate, entity.ForceCreate, warnings);
        existing.RenderLiquid = KeepFirst(existing.Name, "renderliquid", existing.RenderLiquid, entity.RenderLiquid, warnings);
        existing.FetchXmlFilter = KeepFirst(existing.Name, "filter", existing.FetchXmlFilter, entity.FetchXmlFilter, warnings);

        foreach (var field in entity.Fields.Where(f => existing.FindField(f.Name) is null).ToList()) existing.Fields.Add(field);

        var relationships = entity.Relationships.Where(r => !existing.Relationships.Any(e => e.Name == r.Name)).ToList();
        foreach (var relationship in relationships) existing.Relationships.Add(relationship);
        return existing;
    }

    private static T? KeepFirst<T>(string entity, string attribute, T? first, T? next, ICollection<string>? warnings)
    {
        if (first is null) return next;
        if (next is not null && !EqualityComparer<T>.Default.Equals(first, next)) warnings?.Add($"Entity '{entity}': packages disagree on {attribute} ('{first}' and '{next}'); the first package's value is kept.");
        return first;
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
        if (entity is null) return false;

        target.Entities.Remove(entity);
        while (target.EntityImportOrder.Remove(entityLogicalName)) { }

        foreach (var other in target.Entities)
        {
            var pointing = other.Relationships.Where(r => r.ReferencedEntity == entityLogicalName || r.M2mTargetEntity == entityLogicalName).ToList();
            foreach (var relationship in pointing) other.Relationships.Remove(relationship);
        }

        return true;
    }

    /// <summary>
    /// Orders the declared entities so that every entity comes after the entities it looks up (see <see cref="ReferencedEntities"/>),
    /// writes the result to <see cref="CmtDataSchema.EntityImportOrder"/> and reorders <see cref="CmtDataSchema.Entities"/> to match.
    /// The starting point is the current order (the existing import order, then the element order). Entities listed in
    /// <paramref name="manualOrder"/> swap places only among themselves, taking the slots they already occupy, and keep exactly that
    /// relative order even when a lookup disagrees; the list may be partial. Every other entity stays where it is unless a lookup forces
    /// it to move, so nothing that was not listed jumps ahead of or behind its neighbours. Conflicts between the manual order and lookups,
    /// and lookup cycles, are reported in <paramref name="warnings"/>; self-references are ignored.
    /// </summary>
    public static void ResolveImportOrder(CmtDataSchema target, ICollection<string> warnings, IEnumerable<string>? manualOrder = null)
    {
        var manual = new List<string>();
        foreach (var name in (manualOrder ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal))
        {
            if (target.FindEntity(name) is null) warnings.Add($"The manual import order names '{name}', which the data schema does not declare; it is ignored.");
            else manual.Add(name);
        }

        var current = target.EntityImportOrder
            .Where(name => target.FindEntity(name) is not null)
            .Concat(target.Entities.Select(e => e.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Listed entities are refilled into the slots they occupy, in the listed order; everything else keeps its slot.
        var rank = manual.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);
        var nextListed = new Queue<string>(manual);
        var slots = current.Select(name => rank.ContainsKey(name) ? nextListed.Dequeue() : name).ToList();
        var position = slots.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);

        var parents = slots.ToDictionary(
            name => name,
            name => new HashSet<string>(ReferencedEntities(target.FindEntity(name)!).Where(position.ContainsKey), StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var child in manual)
        {
            foreach (var parent in parents[child].Where(p => rank.TryGetValue(p, out var parentRank) && parentRank > rank[child]).ToList())
            {
                parents[child].Remove(parent);
                warnings.Add($"'{child}' is imported before '{parent}', which it looks up, because the manual import order says so.");
            }
        }

        for (var i = 1; i < manual.Count; i++) parents[manual[i]].Add(manual[i - 1]);

        // Always take the earliest slot whose parents are already imported; when only a lookup cycle is left, break it at the earliest
        // slot that does not jump ahead of the manual order.
        var ordered = new List<string>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var remaining = new List<string>(slots);
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(name => parents[name].All(emitted.Contains));
            if (next is null)
            {
                next = remaining.First(name => !rank.TryGetValue(name, out var r) || r == 0 || emitted.Contains(manual[r - 1]));
                foreach (var parent in parents[next].Where(p => !emitted.Contains(p)))
                {
                    warnings.Add($"Entities '{next}' and '{parent}' look each other up (directly or through other entities); '{next}' is imported first and CMT's second pass fills in the lookup.");
                }
            }

            remaining.Remove(next);
            emitted.Add(next);
            ordered.Add(next);
        }

        target.EntityImportOrder.Clear();
        foreach (var name in ordered) target.EntityImportOrder.Add(name);

        var entities = ordered.Select(name => target.FindEntity(name)!).ToList();
        target.Entities.Clear();
        foreach (var entity in entities) target.Entities.Add(entity);
    }

    /// <summary>
    /// Names of the other entities <paramref name="entity"/> looks up: N:1 relationship targets and the <c>lookupType</c> tables of its
    /// entityreference/customer fields (<c>account|contact</c> counts both). These must be imported first.
    /// </summary>
    public static IReadOnlyList<string> ReferencedEntities(CmtSchemaEntity entity)
    {
        var fromRelationships = entity.Relationships
            .Where(r => !r.IsManyToMany && !string.IsNullOrEmpty(r.ReferencedEntity))
            .Select(r => r.ReferencedEntity!);
        var fromLookups = entity.Fields
            .Where(f => LookupFieldTypes.Contains(f.Type) && !string.IsNullOrEmpty(f.LookupType))
            .SelectMany(f => f.LookupType!.Split('|'));

        return fromRelationships.Concat(fromLookups)
            .Select(name => name.Trim())
            .Where(name => name.Length > 0 && name != entity.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
