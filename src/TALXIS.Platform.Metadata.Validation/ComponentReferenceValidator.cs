using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Checks that forms, views, site maps and app modules reference entities, columns, relationships and views that exist.
/// Workspace-scoped like <see cref="RelationshipValidator"/>: a form in one solution may bind to an entity, relationship or
/// view that ships in another solution of the same workspace.
/// <para>
/// To report nothing on valid solutions, a reference is only checked against what the workspace fully defines. Columns are
/// checked only on custom entities a workspace solution fully includes, after allowing the columns the platform adds to every
/// table. A reference to an entity the workspace does not define (a system table, an entity from a package outside the
/// workspace) is skipped, unless it looks like a misspelling of a workspace entity.
/// </para>
/// <para>
/// Findings point at the element in the component file. The validator re-reads each form, view, site map and app module file
/// with line information; for components built in memory it falls back to the model and reports without a line.
/// </para>
/// </summary>
public sealed class ComponentReferenceValidator
{
    private const string SubgridClassId = "E7A81278-8635-4D9E-8D4D-59480B391C5B";
    private const string QuickViewClassId = "5C5600E0-1D6E-4205-A272-BE80DA87FD42";

    // A view URL with viewtype=4230 points at a personal view (userquery), which never ships in a solution.
    private const string UserQueryViewType = "4230";

    /// <summary>Validates one loaded solution against itself.</summary>
    /// <param name="workspace">Loaded solution workspace.</param>
    public IReadOnlyList<ValidationResult> Validate(Workspace workspace)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));
        return Validate(new[] { workspace });
    }

    /// <summary>
    /// Validates every solution's forms, views, site maps and app modules against what all of the solutions define together.
    /// </summary>
    /// <param name="solutions">The solutions of one workspace, each loaded on its own.</param>
    public IReadOnlyList<ValidationResult> Validate(IReadOnlyList<Workspace> solutions)
    {
        if (solutions == null) throw new ArgumentNullException(nameof(solutions));

        var context = new Context(ComponentReferenceIndex.Build(solutions));
        foreach (var solution in solutions)
        {
            foreach (var form in solution.Forms) ValidateForm(form, context);
            foreach (var view in solution.Views) ValidateView(view, context);
            foreach (var siteMap in solution.SiteMaps) ValidateSiteMap(siteMap, context);
            foreach (var appModule in solution.AppModules) ValidateAppModule(appModule, context);
        }

        return context.Results;
    }

    // ---- Forms ----

    private static void ValidateForm(FormMetadata form, Context context)
    {
        var formElement = context.Documents.Load(form.Source?.FilePath)?.Root?
            .Elements("systemform")
            .FirstOrDefault(sf => SameId(sf.Element("formid")?.Value, form.FormId))?
            .Element("form");
        if (formElement == null && form.Body != null)
            formElement = MergeableNodeXmlConverter.ToXElement(form.Body);
        if (formElement == null) return;

        var entityName = form.EntityLogicalName ?? "";
        var label = $"Form '{DisplayName(form.DisplayName, form.FormId)}' ({entityName}, {form.FormType ?? "form"})";
        var controls = formElement.Descendants("control").ToList();

        ValidateControlColumns(form, label, entityName, controls, context);
        ValidateDuplicateControlIds(form, label, controls, context);
        ValidateDuplicateBodyColumns(form, label, formElement, context);

        foreach (var subgrid in controls.Where(c => IsClass(c, SubgridClassId)))
            ValidateSubgrid(form, label, entityName, subgrid, context);
    }

    private static void ValidateControlColumns(FormMetadata form, string label, string entityName, List<XElement> controls, Context context)
    {
        if (!context.Index.IsComplete(entityName)) return;

        foreach (var control in controls)
        {
            var field = control.Attribute("datafieldname");
            var column = field?.Value.Trim();
            if (string.IsNullOrEmpty(column) || context.Index.HasColumn(entityName, column!)) continue;

            context.Add(ValidationSeverity.Error, ValidationDiagnostics.FormControlColumnUnknown, form, field,
                $"{label}: control '{control.Attribute("id")?.Value}' binds datafieldname '{column}', which is not a column of '{entityName}'." +
                NameSuggestion.Hint(column!, context.Index.ColumnsOf(entityName)) +
                $" Correct the datafieldname or add the column to '{entityName}'.");
        }
    }

    private static void ValidateDuplicateControlIds(FormMetadata form, string label, List<XElement> controls, Context context)
    {
        foreach (var group in controls
                     .Select(c => c.Attribute("id"))
                     .Where(id => !string.IsNullOrWhiteSpace(id?.Value))
                     .GroupBy(id => id!.Value, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            var first = group.First()!;
            foreach (var duplicate in group.Skip(1))
            {
                context.Add(ValidationSeverity.Error, ValidationDiagnostics.FormControlIdDuplicate, form, duplicate,
                    $"{label}: control id '{group.Key}' is used {group.Count()} times{AtLine(first, " (first at line ", ")")}. " +
                    "Control ids must be unique within a form; give this control a new id (Dataverse numbers repeated columns 'name1', 'name2') or remove it.");
            }
        }
    }

    // Dataverse lets a column appear more than once on a form (the instances get ids 'name', 'name1', ...), and the header or
    // footer repeating a body column is the common case, so only repeats within the body are flagged, and only as a warning.
    // A quick view control carries the datafieldname of the lookup it follows, so it never counts as a repeat of that lookup.
    private static void ValidateDuplicateBodyColumns(FormMetadata form, string label, XElement formElement, Context context)
    {
        var tabs = formElement.Element("tabs");
        if (tabs == null) return;

        foreach (var group in tabs.Descendants("control")
                     .Where(c => !IsClass(c, QuickViewClassId))
                     .Select(c => c.Attribute("datafieldname"))
                     .Where(f => !string.IsNullOrWhiteSpace(f?.Value))
                     .GroupBy(f => f!.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            var first = group.First()!;
            context.Add(ValidationSeverity.Warning, ValidationDiagnostics.FormColumnRepeated, form, group.ElementAt(1),
                $"{label}: column '{group.Key}' is placed {group.Count()} times in the form body{AtLine(first, " (first at line ", ")")}. " +
                "Dataverse allows this, but it is usually an accidental copy; remove the extra placement unless it is intended.");
        }
    }

    private static void ValidateSubgrid(FormMetadata form, string label, string formEntity, XElement subgrid, Context context)
    {
        var parameters = subgrid.Element("parameters");
        var targetElement = parameters?.Element("TargetEntityType");
        var target = targetElement?.Value.Trim();
        if (string.IsNullOrEmpty(target)) return;

        var subgridLabel = $"{label}: subgrid '{subgrid.Attribute("id")?.Value}'";

        if (!context.Index.IsKnownEntity(target!))
        {
            var misspelled = context.Index.MisspelledEntity(target!);
            if (misspelled != null)
            {
                context.Add(ValidationSeverity.Error, ValidationDiagnostics.SubgridTargetEntityUnknown, form, targetElement,
                    $"{subgridLabel} has TargetEntityType '{target}', which is not an entity in the workspace. Did you mean '{misspelled}'? " +
                    "Set TargetEntityType to the logical name of the entity the subgrid lists.");
            }
            return;
        }

        ValidateSubgridRelationship(form, subgridLabel, formEntity, target!, parameters!.Element("RelationshipName"), context);

        if (!string.Equals(parameters.Element("IsUserView")?.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            ValidateSubgridView(form, subgridLabel, target!, parameters.Element("ViewId"), context);
    }

    private static void ValidateSubgridRelationship(FormMetadata form, string subgridLabel, string formEntity, string target, XElement? element, Context context)
    {
        var name = element?.Value.Trim();
        // No relationship means the subgrid lists all records of the target, which is valid.
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(formEntity)) return;

        var candidates = context.Index.SubgridRelationships(formEntity, target).ToList();
        var hint = candidates.Count == 1
            ? $" Did you mean '{candidates[0]}'?"
            : NameSuggestion.Hint(name!, candidates, maxDistance: int.MaxValue);
        var expected = $"It must be a one-to-many relationship from '{formEntity}' (referenced) to '{target}' (referencing), or a many-to-many between them.";

        var relationships = context.Index.FindRelationships(name!);
        if (relationships.Count > 0)
        {
            if (relationships.Any(r => ComponentReferenceIndex.ConnectsSubgrid(r, formEntity, target))
                || !relationships.All(ComponentReferenceIndex.IsResolved)) return;

            context.Add(ValidationSeverity.Error, ValidationDiagnostics.SubgridRelationshipInvalid, form, element,
                $"{subgridLabel} uses RelationshipName '{name}', which is {Describe(relationships[0])}. {expected}{hint}" +
                " Point RelationshipName at a relationship that links the form's entity to the subgrid's target.");
            return;
        }

        // The relationship may ship outside the workspace unless the workspace owns both entities completely.
        if (!context.Index.IsComplete(formEntity) || !context.Index.IsComplete(target)) return;

        context.Add(ValidationSeverity.Error, ValidationDiagnostics.SubgridRelationshipInvalid, form, element,
            $"{subgridLabel} uses RelationshipName '{name}', which is not defined in the workspace. {expected}{hint}" +
            " Correct the name or add the relationship.");
    }

    private static void ValidateSubgridView(FormMetadata form, string subgridLabel, string target, XElement? element, Context context)
    {
        var viewId = element?.Value.Trim();
        if (ComponentReferenceIndex.NormalizeId(viewId) == null) return;

        var view = context.Index.FindView(viewId);
        if (view != null)
        {
            if (SameName(view.EntityLogicalName, target)) return;

            context.Add(ValidationSeverity.Error, ValidationDiagnostics.SubgridViewInvalid, form, element,
                $"{subgridLabel} uses ViewId {viewId} ('{DisplayName(view.DisplayName, view.SavedQueryId)}'), which is a view of '{view.EntityLogicalName}', not of the target '{target}'." +
                ViewsHint(target, context) + $" Set ViewId to a saved query of '{target}'.");
            return;
        }

        if (!context.Index.IsComplete(target)) return;

        context.Add(ValidationSeverity.Error, ValidationDiagnostics.SubgridViewInvalid, form, element,
            $"{subgridLabel} uses ViewId {viewId}, which is not a saved query of '{target}' in the workspace." +
            ViewsHint(target, context) + $" Set ViewId to a saved query of '{target}' or add the view.");
    }

    // ---- Views ----

    private static void ValidateView(SavedQueryMetadata view, Context context)
    {
        var savedQuery = context.Documents.Load(view.Source?.FilePath)?.Root?
            .Elements("savedquery")
            .FirstOrDefault(sq => SameId(sq.Element("savedqueryid")?.Value, view.SavedQueryId));

        var fetch = savedQuery?.Element("fetchxml")?.Element("fetch") ?? ParseFragment(view.FetchXml);
        var entityElement = fetch?.Element("entity");
        var rootEntity = entityElement?.Attribute("name")?.Value.Trim();
        if (string.IsNullOrEmpty(rootEntity)) rootEntity = view.EntityLogicalName;
        if (string.IsNullOrEmpty(rootEntity)) return;

        var label = $"View '{DisplayName(view.DisplayName, view.SavedQueryId)}' ({view.EntityLogicalName})";
        var aliases = fetch == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : fetch.Descendants("link-entity")
                .Where(l => !string.IsNullOrWhiteSpace(l.Attribute("alias")?.Value) && !string.IsNullOrWhiteSpace(l.Attribute("name")?.Value))
                .GroupBy(l => l.Attribute("alias")!.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Attribute("name")!.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        if (entityElement != null)
            ValidateFetchEntity(view, label, entityElement, rootEntity!, aliases, context);

        var grid = savedQuery?.Element("layoutxml")?.Element("grid") ?? ParseFragment(view.LayoutXml);
        if (grid != null)
            ValidateLayout(view, label, grid, rootEntity!, aliases, context);
    }

    private static void ValidateFetchEntity(SavedQueryMetadata view, string label, XElement entityElement, string entityName,
        Dictionary<string, string> aliases, Context context)
    {
        foreach (var child in entityElement.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "attribute":
                    CheckFetchColumn(view, label, "<attribute name>", entityName, child.Attribute("name"), context);
                    break;
                case "order":
                    // An order by alias (aggregate queries) has no attribute to check.
                    CheckFetchColumn(view, label, "<order attribute>", entityName, child.Attribute("attribute"), context);
                    break;
                case "filter":
                    ValidateFetchFilter(view, label, child, entityName, aliases, context);
                    break;
                case "link-entity":
                    var linked = child.Attribute("name")?.Value.Trim();
                    if (!string.IsNullOrEmpty(linked))
                        ValidateFetchEntity(view, label, child, linked!, aliases, context);
                    break;
            }
        }
    }

    private static void ValidateFetchFilter(SavedQueryMetadata view, string label, XElement filter, string entityName,
        Dictionary<string, string> aliases, Context context)
    {
        foreach (var child in filter.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "condition":
                    // entityname="alias" applies the condition to a linked entity; an alias we cannot resolve is skipped.
                    var alias = child.Attribute("entityname")?.Value.Trim();
                    if (string.IsNullOrEmpty(alias))
                        CheckFetchColumn(view, label, "<condition attribute>", entityName, child.Attribute("attribute"), context);
                    else if (aliases.TryGetValue(alias!, out var aliased))
                        CheckFetchColumn(view, label, "<condition attribute>", aliased, child.Attribute("attribute"), context);
                    break;
                case "filter":
                    ValidateFetchFilter(view, label, child, entityName, aliases, context);
                    break;
                case "link-entity":
                    var linked = child.Attribute("name")?.Value.Trim();
                    if (!string.IsNullOrEmpty(linked))
                        ValidateFetchEntity(view, label, child, linked!, aliases, context);
                    break;
            }
        }
    }

    private static void CheckFetchColumn(SavedQueryMetadata view, string label, string what, string entityName, XAttribute? attribute, Context context)
    {
        var column = attribute?.Value.Trim();
        if (string.IsNullOrEmpty(column) || !context.Index.IsComplete(entityName) || context.Index.HasColumn(entityName, column!)) return;

        context.Add(ValidationSeverity.Error, ValidationDiagnostics.ViewFetchColumnUnknown, view, attribute,
            $"{label}: fetchxml {what} '{column}' is not a column of '{entityName}'." +
            NameSuggestion.Hint(column!, context.Index.ColumnsOf(entityName)) +
            $" Correct the name in the fetchxml or add the column to '{entityName}'.");
    }

    private static void ValidateLayout(SavedQueryMetadata view, string label, XElement grid, string rootEntity,
        Dictionary<string, string> aliases, Context context)
    {
        foreach (var cell in grid.Descendants("cell"))
        {
            var nameAttribute = cell.Attribute("name");
            var name = nameAttribute?.Value.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var entityName = rootEntity;
            var column = name!;
            var dot = name!.IndexOf('.');
            if (dot >= 0)
            {
                // "alias.column" reads a link-entity column; skip it when the alias is not in the fetchxml.
                if (!aliases.TryGetValue(name.Substring(0, dot), out entityName)) continue;
                column = name.Substring(dot + 1);
            }

            if (!context.Index.IsComplete(entityName) || context.Index.HasColumn(entityName, column)) continue;

            context.Add(ValidationSeverity.Error, ValidationDiagnostics.ViewLayoutColumnUnknown, view, nameAttribute,
                $"{label}: layoutxml <cell name='{name}'> is not a column of '{entityName}'." +
                NameSuggestion.Hint(column, context.Index.ColumnsOf(entityName)) +
                $" Correct the cell name or add the column to '{entityName}'.");
        }
    }

    // ---- Site maps ----

    private static readonly Regex UrlParameter = new(@"[?&](?<key>etn|viewid|viewtype)=(?<value>[^&#]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void ValidateSiteMap(SiteMapMetadata siteMap, Context context)
    {
        var root = context.Documents.Load(siteMap.Source?.FilePath)?.Root;
        if (root == null && siteMap.Body != null)
            root = MergeableNodeXmlConverter.ToXElement(siteMap.Body);
        if (root == null) return;

        var label = $"Site map '{siteMap.UniqueName}'";
        foreach (var subArea in root.DescendantsAndSelf("SubArea"))
        {
            var subAreaLabel = $"{label}: SubArea '{subArea.Attribute("Id")?.Value}'";

            var entityAttribute = subArea.Attribute("Entity");
            var entity = entityAttribute?.Value.Trim();
            if (!string.IsNullOrEmpty(entity))
                CheckSiteMapEntity(siteMap, subAreaLabel, "Entity", entity!, entityAttribute, context);

            var urlAttribute = subArea.Attribute("Url");
            if (urlAttribute != null)
                ValidateSiteMapUrl(siteMap, subAreaLabel, urlAttribute, context);
        }
    }

    private static bool CheckSiteMapEntity(SiteMapMetadata siteMap, string label, string where, string entity, XObject? at, Context context)
    {
        if (context.Index.IsKnownEntity(entity)) return true;

        var misspelled = context.Index.MisspelledEntity(entity);
        if (misspelled != null)
        {
            context.Add(ValidationSeverity.Error, ValidationDiagnostics.SiteMapEntityUnknown, siteMap, at,
                $"{label} {where} names entity '{entity}', which is not an entity in the workspace. Did you mean '{misspelled}'? " +
                "Use the logical name of an existing entity.");
        }

        return false;
    }

    private static void ValidateSiteMapUrl(SiteMapMetadata siteMap, string label, XAttribute urlAttribute, Context context)
    {
        var parameters = UrlParameter.Matches(urlAttribute.Value).Cast<Match>()
            .GroupBy(m => m.Groups["key"].Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Uri.UnescapeDataString(g.First().Groups["value"].Value).Trim(), StringComparer.OrdinalIgnoreCase);

        if (!parameters.TryGetValue("etn", out var entity) || string.IsNullOrEmpty(entity)) return;
        if (!CheckSiteMapEntity(siteMap, label, "Url etn=", entity, urlAttribute, context)) return;

        if (!parameters.TryGetValue("viewid", out var viewId) || ComponentReferenceIndex.NormalizeId(viewId) == null) return;
        if (parameters.TryGetValue("viewtype", out var viewType) && viewType == UserQueryViewType) return;

        var view = context.Index.FindView(viewId);
        if (view != null)
        {
            if (SameName(view.EntityLogicalName, entity)) return;

            context.Add(ValidationSeverity.Error, ValidationDiagnostics.SiteMapViewInvalid, siteMap, urlAttribute,
                $"{label} Url opens view {viewId} ('{DisplayName(view.DisplayName, view.SavedQueryId)}') for etn={entity}, but that view belongs to '{view.EntityLogicalName}'." +
                ViewsHint(entity, context) + $" Use a saved query of '{entity}' in viewid.");
            return;
        }

        if (!context.Index.IsComplete(entity)) return;

        context.Add(ValidationSeverity.Error, ValidationDiagnostics.SiteMapViewInvalid, siteMap, urlAttribute,
            $"{label} Url opens view {viewId} for etn={entity}, but no saved query of '{entity}' with that id is in the workspace." +
            ViewsHint(entity, context) + $" Use a saved query of '{entity}' in viewid or add the view.");
    }

    // ---- App modules ----

    private static void ValidateAppModule(AppModuleMetadata appModule, Context context)
    {
        var root = context.Documents.Load(appModule.Source?.FilePath)?.Root;
        if (root == null && appModule.Body != null)
            root = MergeableNodeXmlConverter.ToXElement(appModule.Body);
        var components = root?.Element("AppModuleComponents")?.Elements("AppModuleComponent").ToList();
        if (components == null) return;

        var label = $"App module '{appModule.UniqueName}'";
        var entities = components
            .Where(c => TypeOf(c) == ComponentType.Entity)
            .Select(c => c.Attribute("schemaName")?.Value.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        // Forms and views are referenced by id only. A missing one can only be told apart from a system or package form when
        // every entity of the app is fully defined in the workspace.
        var formsAndViewsAreLocal = entities.All(context.Index.IsComplete);

        foreach (var component in components)
        {
            var schemaName = component.Attribute("schemaName")?.Value.Trim();
            var id = component.Attribute("id")?.Value.Trim();

            switch (TypeOf(component))
            {
                case ComponentType.Entity when !string.IsNullOrEmpty(schemaName):
                    var misspelled = context.Index.MisspelledEntity(schemaName!);
                    if (misspelled != null)
                    {
                        context.Add(ValidationSeverity.Warning, ValidationDiagnostics.AppModuleComponentMissing, appModule, component,
                            $"{label} includes entity '{schemaName}', which is not an entity in the workspace. Did you mean '{misspelled}'? " +
                            "Correct the schemaName or remove the component.");
                    }
                    break;

                case ComponentType.SystemForm when formsAndViewsAreLocal && ComponentReferenceIndex.NormalizeId(id) != null && !context.Index.HasForm(id):
                    context.Add(ValidationSeverity.Warning, ValidationDiagnostics.AppModuleComponentMissing, appModule, component,
                        $"{label} includes form {id}, which is not a form of any entity in the workspace. Remove the component or add the form.");
                    break;

                case ComponentType.SavedQuery when formsAndViewsAreLocal && ComponentReferenceIndex.NormalizeId(id) != null && context.Index.FindView(id) == null:
                    context.Add(ValidationSeverity.Warning, ValidationDiagnostics.AppModuleComponentMissing, appModule, component,
                        $"{label} includes view {id}, which is not a saved query of any entity in the workspace. Remove the component or add the view.");
                    break;

                case ComponentType.SiteMap when !string.IsNullOrEmpty(schemaName) && !context.Index.HasSiteMap(schemaName!):
                    context.Add(ValidationSeverity.Warning, ValidationDiagnostics.AppModuleComponentMissing, appModule, component,
                        $"{label} uses site map '{schemaName}', which is not in the workspace. Add AppModuleSiteMaps/{schemaName} or correct the schemaName.");
                    break;
            }
        }
    }

    // ---- Helpers ----

    private static ComponentType? TypeOf(XElement component) =>
        int.TryParse(component.Attribute("type")?.Value, out var type) ? (ComponentType)type : null;

    private static string ViewsHint(string entityName, Context context)
    {
        var views = context.Index.ViewsOf(entityName).Take(5)
            .Select(v => $"'{DisplayName(v.DisplayName, v.SavedQueryId)}' {v.SavedQueryId}")
            .ToList();
        return views.Count == 0 ? "" : $" Views of '{entityName}' in the workspace: {string.Join(", ", views)}.";
    }

    private static string Describe(RelationshipMetadata relationship) => relationship switch
    {
        OneToManyRelationshipMetadata o => $"a one-to-many from '{o.ReferencedEntity}' to '{o.ReferencingEntity}'",
        ManyToManyRelationshipMetadata m => $"a many-to-many between '{m.Entity1LogicalName}' and '{m.Entity2LogicalName}'",
        _ => "not a relationship a subgrid can use"
    };

    private static bool IsClass(XElement control, string classId) =>
        string.Equals(control.Attribute("classid")?.Value.Trim().Trim('{', '}'), classId, StringComparison.OrdinalIgnoreCase);

    private static bool SameId(string? a, string? b)
    {
        var left = ComponentReferenceIndex.NormalizeId(a);
        return left != null && left == ComponentReferenceIndex.NormalizeId(b);
    }

    private static bool SameName(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string DisplayName(Label label, string fallback) => string.IsNullOrWhiteSpace(label.Default) ? fallback : label.Default!;

    private static string AtLine(XObject at, string prefix, string suffix) =>
        at is IXmlLineInfo info && info.HasLineInfo() ? $"{prefix}{info.LineNumber}{suffix}" : "";

    // Models built in memory may carry fetchxml or layoutxml as a string rather than nested elements.
    private static XElement? ParseFragment(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml) || xml!.TrimStart()[0] != '<') return null;
        try
        {
            return XElement.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private sealed class Context
    {
        public Context(ComponentReferenceIndex index) => Index = index;

        public ComponentReferenceIndex Index { get; }
        public SourceDocuments Documents { get; } = new();
        public List<ValidationResult> Results { get; } = new();

        /// <summary>Adds a finding located at <paramref name="at"/>, or at the component when the XML has no line information.</summary>
        public void Add(ValidationSeverity severity, string code, MetadataBase component, XObject? at, string message)
        {
            var source = component.Source;
            int? line = source?.Line, column = source?.Column;
            if (at is IXmlLineInfo info && info.HasLineInfo())
            {
                line = info.LineNumber;
                column = info.LinePosition;
            }

            var path = string.IsNullOrEmpty(source?.FilePath) ? null : source!.FilePath;
            Results.Add(new ValidationResult(severity, message, path, line, column) { Code = code });
        }
    }

    /// <summary>Component files loaded once each with line information. Unreadable files are left to the schema and load stages.</summary>
    private sealed class SourceDocuments
    {
        private readonly Dictionary<string, XDocument?> _documents = new(StringComparer.Ordinal);

        public XDocument? Load(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_documents.TryGetValue(path!, out var cached)) return cached;

            XDocument? document = null;
            try
            {
                if (File.Exists(path)) document = XDocument.Load(path, LoadOptions.SetLineInfo);
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                document = null;
            }

            _documents[path!] = document;
            return document;
        }
    }
}
