using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// What every solution in a workspace defines, so a form, view, site map or app in one solution can be checked against
/// entities, columns, relationships and views that ship in another. Knows which entities it has a complete column list for:
/// only those are checked column by column, everything else (system tables, entities from packages outside the workspace,
/// shells) is skipped.
/// </summary>
internal sealed class ComponentReferenceIndex
{
    // Columns Dataverse adds to tables beyond DataverseSystemColumns: business process flow, entity image and elastic table columns.
    private static readonly HashSet<string> PlatformColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "processid", "stageid", "traversedpath", "entityimage", "entityimageid", "entityimage_url", "entityimage_timestamp",
        "partitionid", "ttlinseconds"
    };

    // Columns every activity table inherits from activitypointer; a trimmed Entity.xml of a custom activity may omit them.
    private static readonly HashSet<string> ActivityColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "activityid", "activitytypecode", "activityadditionalparams", "actualdurationminutes", "actualend", "actualstart",
        "bcc", "cc", "community", "customers", "deliverylastattemptedon", "deliveryprioritycode", "description",
        "exchangeitemid", "exchangeweblink", "from", "instancetypecode", "isbilled", "ismapiprivate", "isregularactivity",
        "isworkflowcreated", "lastonholdtime", "leftvoicemail", "onholdtime", "optionalattendees", "organizer", "partners",
        "postponeactivityprocessinguntil", "prioritycode", "regardingobjectid", "regardingobjecttypecode", "requiredattendees",
        "resources", "scheduleddurationminutes", "scheduledend", "scheduledstart", "senton", "sendermailboxid", "seriesid",
        "slaid", "slainvokedid", "sortdate", "subject", "to"
    };

    // Names Dataverse derives from a column: lookup name helpers, money base values, file and image companions.
    private static readonly string[] DerivedColumnSuffixes = { "yominame", "name", "type", "_base", "_name", "_url", "_timestamp" };

    private readonly Dictionary<string, EntityEntry> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownEntityNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _prefixes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RelationshipMetadata>> _relationships = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedQueryMetadata> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _forms = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _siteMaps = new(StringComparer.OrdinalIgnoreCase);

    public static ComponentReferenceIndex Build(IEnumerable<Workspace> solutions)
    {
        var index = new ComponentReferenceIndex();
        foreach (var solution in solutions)
            index.Add(solution);
        return index;
    }

    private void Add(Workspace solution)
    {
        var shells = RelationshipValidator.BuildShellEntitySet(solution);

        foreach (var entity in solution.Entities)
        {
            if (!_entities.TryGetValue(entity.LogicalName, out var entry))
                _entities[entity.LogicalName] = entry = new EntityEntry(entity.LogicalName);

            foreach (var attribute in entity.Attributes)
                entry.Columns.Add(attribute.LogicalName);
            entry.IsActivity |= entity.IsActivity;
            // A system table's Entity.xml may be trimmed to the columns a solution touches, and a shell carries only some
            // columns, so only a custom entity this solution fully includes has a column list complete enough to check against.
            entry.IsComplete |= entity.IsCustomEntity && !shells.Contains(entity.LogicalName);

            _knownEntityNames.Add(entity.LogicalName);
            var separator = entity.LogicalName.IndexOf('_');
            if (entity.IsCustomEntity && separator > 0)
                _prefixes.Add(entity.LogicalName.Substring(0, separator));
        }

        foreach (var manifest in solution.Solutions)
        {
            if (!string.IsNullOrWhiteSpace(manifest.Publisher?.Prefix))
                _prefixes.Add(manifest.Publisher!.Prefix);

            foreach (var component in manifest.RootComponents)
            {
                if (component.Type == ComponentType.Entity && !string.IsNullOrWhiteSpace(component.SchemaName))
                    _knownEntityNames.Add(component.SchemaName!);
            }
        }

        foreach (var relationship in solution.Relationships)
        {
            if (!_relationships.TryGetValue(relationship.SchemaName, out var list))
                _relationships[relationship.SchemaName] = list = new List<RelationshipMetadata>();
            list.Add(relationship);

            foreach (var end in RelationshipEnds(relationship))
            {
                if (!string.IsNullOrWhiteSpace(end))
                    _knownEntityNames.Add(end);
            }
        }

        foreach (var view in solution.Views)
        {
            var id = NormalizeId(view.SavedQueryId);
            if (id != null && !_views.ContainsKey(id))
                _views[id] = view;
        }

        foreach (var form in solution.Forms)
        {
            var id = NormalizeId(form.FormId);
            if (id != null)
                _forms.Add(id);
        }

        foreach (var siteMap in solution.SiteMaps)
            _siteMaps.Add(siteMap.UniqueName);
    }

    /// <summary>Whether the workspace defines, declares or relates to an entity of this name.</summary>
    public bool IsKnownEntity(string name) => _knownEntityNames.Contains(name);

    /// <summary>Whether the workspace holds a complete column list for the entity, so a missing column is a real defect.</summary>
    public bool IsComplete(string? entityName) =>
        !string.IsNullOrWhiteSpace(entityName) && _entities.TryGetValue(entityName!, out var entry) && entry.IsComplete;

    /// <summary>
    /// For a name the workspace does not know, the workspace entity it most likely misspells: same publisher prefix and at
    /// most two edits away. A name that matches nothing is left alone, since it may be a system table or come from a package
    /// outside the workspace.
    /// </summary>
    public string? MisspelledEntity(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || IsKnownEntity(name)) return null;

        var separator = name.IndexOf('_');
        if (separator <= 0 || !_prefixes.Contains(name.Substring(0, separator))) return null;

        return NameSuggestion.Nearest(name, _entities.Keys, maxDistance: 2);
    }

    /// <summary>Whether <paramref name="column"/> exists on a complete entity, counting the columns the platform adds to every table.</summary>
    public bool HasColumn(string entityName, string column)
    {
        if (!_entities.TryGetValue(entityName, out var entry)) return true;
        return HasColumn(entry, column, allowDerived: true);
    }

    /// <summary>Columns of the entity, for "did you mean" hints.</summary>
    public IEnumerable<string> ColumnsOf(string entityName) =>
        _entities.TryGetValue(entityName, out var entry) ? entry.Columns : Enumerable.Empty<string>();

    public IReadOnlyList<RelationshipMetadata> FindRelationships(string schemaName) =>
        _relationships.TryGetValue(schemaName, out var list) ? list : (IReadOnlyList<RelationshipMetadata>)Array.Empty<RelationshipMetadata>();

    /// <summary>Relationships a subgrid on <paramref name="formEntity"/> can use to list <paramref name="targetEntity"/> records.</summary>
    public IEnumerable<string> SubgridRelationships(string formEntity, string targetEntity) =>
        _relationships.Values.SelectMany(l => l)
            .Where(r => ConnectsSubgrid(r, formEntity, targetEntity))
            .Select(r => r.SchemaName)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public SavedQueryMetadata? FindView(string? id)
    {
        var normalized = NormalizeId(id);
        return normalized != null && _views.TryGetValue(normalized, out var view) ? view : null;
    }

    public IEnumerable<SavedQueryMetadata> ViewsOf(string entityName) =>
        _views.Values.Where(v => string.Equals(v.EntityLogicalName, entityName, StringComparison.OrdinalIgnoreCase));

    public bool HasForm(string? id)
    {
        var normalized = NormalizeId(id);
        return normalized != null && _forms.Contains(normalized);
    }

    public bool HasSiteMap(string uniqueName) => _siteMaps.Contains(uniqueName);

    /// <summary>
    /// Whether a subgrid on <paramref name="formEntity"/> showing <paramref name="targetEntity"/> records can use the relationship:
    /// a one-to-many whose referenced (one) side is the form's entity and referencing (many) side is the target, or a
    /// many-to-many between the two.
    /// </summary>
    public static bool ConnectsSubgrid(RelationshipMetadata relationship, string formEntity, string targetEntity) => relationship switch
    {
        OneToManyRelationshipMetadata oneToMany =>
            Same(oneToMany.ReferencedEntity, formEntity) && Same(oneToMany.ReferencingEntity, targetEntity),
        ManyToManyRelationshipMetadata manyToMany =>
            (Same(manyToMany.Entity1LogicalName, formEntity) && Same(manyToMany.Entity2LogicalName, targetEntity)) ||
            (Same(manyToMany.Entity2LogicalName, formEntity) && Same(manyToMany.Entity1LogicalName, targetEntity)),
        _ => false
    };

    /// <summary>Whether the relationship names its entities; one declared by name only is reported by <see cref="RelationshipValidator"/>.</summary>
    public static bool IsResolved(RelationshipMetadata relationship) =>
        RelationshipEnds(relationship).All(end => !string.IsNullOrWhiteSpace(end));

    public static IEnumerable<string> RelationshipEnds(RelationshipMetadata relationship) => relationship switch
    {
        OneToManyRelationshipMetadata oneToMany => new[] { oneToMany.ReferencedEntity, oneToMany.ReferencingEntity },
        ManyToManyRelationshipMetadata manyToMany => new[] { manyToMany.Entity1LogicalName, manyToMany.Entity2LogicalName },
        _ => Array.Empty<string>()
    };

    /// <summary>A GUID in its canonical form without braces, or <c>null</c> when the value is not a GUID.</summary>
    public static string? NormalizeId(string? id) =>
        Guid.TryParse(id?.Trim(), out var guid) ? guid.ToString("D") : null;

    private static bool HasColumn(EntityEntry entry, string column, bool allowDerived)
    {
        if (entry.Columns.Contains(column)
            || DataverseSystemColumns.Contains(column)
            || PlatformColumns.Contains(column)
            || string.Equals(column, entry.LogicalName + "id", StringComparison.OrdinalIgnoreCase)
            || (entry.IsActivity && ActivityColumns.Contains(column)))
            return true;

        if (!allowDerived) return false;

        foreach (var suffix in DerivedColumnSuffixes)
        {
            if (column.Length > suffix.Length && column.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && HasColumn(entry, column.Substring(0, column.Length - suffix.Length), allowDerived: false))
                return true;
        }

        return false;
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed class EntityEntry
    {
        public EntityEntry(string logicalName) => LogicalName = logicalName;

        public string LogicalName { get; }
        public HashSet<string> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsActivity { get; set; }
        public bool IsComplete { get; set; }
    }
}
