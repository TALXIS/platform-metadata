using System.Text.RegularExpressions;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Normalizes a freshly exported (unpacked and loaded) solution workspace against its source project
/// to reduce roundtrip noise: server-added system relationships, components outside the source solution,
/// server-enriched attributes, and Managed/Version drift.
/// </summary>
public sealed class ExportNormalizer
{
    private static readonly HashSet<string> SystemRelationshipEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "businessunit", "owner", "systemuser", "team"
    };

    private static readonly Regex SystemRelationshipNamePattern = new(
        "^(business_unit_.+|lk_.+_(createdby|modifiedby)|owner_.+|team_.+|user_.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] ServerVersionAttributeNames =
    {
        "OrganizationVersion", "OrganizationSchemaType", "CRMServerServiceabilityVersion"
    };

    /// <summary>
    /// Normalizes an exported workspace in place against the matching source solution project.
    /// </summary>
    /// <param name="exported">Workspace loaded from an unpacked solution export. Must contain exactly one solution.</param>
    /// <param name="source">Workspace loaded from the source project(s). Must contain a solution with the same unique name.</param>
    /// <param name="options">Rule toggles; all rules run when omitted.</param>
    /// <returns>A report of every applied change.</returns>
    public ExportNormalizationResult Normalize(Workspace exported, Workspace source, ExportNormalizationOptions? options = null)
    {
        if (exported == null) throw new ArgumentNullException(nameof(exported));
        if (source == null) throw new ArgumentNullException(nameof(source));
        options ??= new ExportNormalizationOptions();

        if (exported.Solutions.Count != 1)
            throw new InvalidOperationException("The exported workspace must contain exactly one solution manifest.");

        var exportedSolution = exported.Solutions[0];
        var sourceSolution = source.FindSolution(exportedSolution.UniqueName)
            ?? throw new InvalidOperationException($"The source workspace does not contain solution '{exportedSolution.UniqueName}'.");

        var changes = new List<ExportNormalizationChange>();

        if (options.StripComponentsNotInSource) StripComponentsNotInSource(exported, exportedSolution, source, sourceSolution, changes);
        if (options.EnforceRootComponentBehavior) EnforceRootComponentBehavior(exported, source, sourceSolution, changes);
        if (options.StripComponentsOwnedByOtherSolutions) StripComponentsOwnedByOtherSolutions(exported, exportedSolution, source, sourceSolution, changes);
        if (options.StripSystemRelationships) StripSystemRelationships(exported, source, changes);
        if (options.StripServerVersionAttributes) StripServerVersionAttributes(exported, exportedSolution, changes);
        if (options.NormalizeManagedFlag) NormalizeManagedFlag(exportedSolution, sourceSolution, changes);
        if (options.NormalizeSolutionVersion) NormalizeSolutionVersion(exportedSolution, sourceSolution, changes);

        return new ExportNormalizationResult(changes);
    }

    private static void StripComponentsNotInSource(Workspace exported, Solution exportedSolution, Workspace source, Solution sourceSolution, List<ExportNormalizationChange> changes)
    {
        // A source without declared root components is a bootstrap project (fresh clone/scaffold) —
        // there is no membership truth to enforce yet, so stripping would delete the whole export.
        if (sourceSolution.RootComponents.Count == 0) return;

        foreach (var entity in exported.Entities.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.Entity, entity.LogicalName, id: null)) continue;
            if (source.FindEntity(entity.LogicalName) != null) continue;

            StripEntityWithRelationships(exported, source, entity.LogicalName, changes);
            exportedSolution.RemoveRootComponent(ComponentType.Entity, entity.LogicalName, id: null);
        }

        foreach (var optionSet in exported.GlobalOptionSets.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.OptionSet, optionSet.Name, id: null)) continue;
            if (source.GlobalOptionSets.Any(o => NameEquals(o.Name, optionSet.Name))) continue;

            exported.RemoveGlobalOptionSet(optionSet.Name);
            exportedSolution.RemoveRootComponent(ComponentType.OptionSet, optionSet.Name, id: null);
            RecordStrip(changes, "option set", optionSet.Name, ComponentType.OptionSet, optionSet.Name);
        }

        foreach (var workflow in exported.Workflows.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.Workflow, workflow.UniqueName, workflow.WorkflowId)) continue;
            if (source.Workflows.Any(w => IdEquals(w.WorkflowId, workflow.WorkflowId))) continue;

            exported.RemoveWorkflow(workflow.WorkflowId);
            exportedSolution.RemoveRootComponent(ComponentType.Workflow, workflow.UniqueName, ParseGuid(workflow.WorkflowId));
            RecordStrip(changes, "workflow", workflow.UniqueName ?? workflow.WorkflowId, ComponentType.Workflow, workflow.WorkflowId);
        }

        foreach (var webResource in exported.WebResources.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.WebResource, webResource.Name, webResource.WebResourceId)) continue;
            if (source.WebResources.Any(w => IdEquals(w.WebResourceId, webResource.WebResourceId) || NameEquals(w.Name, webResource.Name))) continue;

            exported.RemoveWebResource(webResource.WebResourceId);
            exportedSolution.RemoveRootComponent(ComponentType.WebResource, webResource.Name, ParseGuid(webResource.WebResourceId));
            RecordStrip(changes, "web resource", webResource.Name, ComponentType.WebResource, webResource.WebResourceId);
        }

        foreach (var role in exported.SecurityRoles.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.Role, role.Name, role.RoleId)) continue;
            if (source.SecurityRoles.Any(r => IdEquals(r.RoleId, role.RoleId))) continue;

            exported.RemoveSecurityRole(role.RoleId);
            exportedSolution.RemoveRootComponent(ComponentType.Role, schemaName: null, ParseGuid(role.RoleId));
            RecordStrip(changes, "security role", role.Name, ComponentType.Role, role.RoleId);
        }

        foreach (var appModule in exported.AppModules.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.AppModule, appModule.UniqueName, id: null)) continue;
            if (source.AppModules.Any(a => NameEquals(a.UniqueName, appModule.UniqueName))) continue;

            exported.RemoveAppModule(appModule.UniqueName);
            exportedSolution.RemoveRootComponent(ComponentType.AppModule, appModule.UniqueName, id: null);
            RecordStrip(changes, "app module", appModule.UniqueName, ComponentType.AppModule, appModule.UniqueName);
        }

        // Site maps are deliberately not filtered: their unique names do not reliably match the
        // schemaName in RootComponents (production data shows mismatches), and an app module's
        // site map may arrive without its own root-component row.

        foreach (var pluginAssembly in exported.PluginAssemblies.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.PluginAssembly, pluginAssembly.Name, pluginAssembly.PluginAssemblyId)) continue;
            if (source.PluginAssemblies.Any(p => IdEquals(p.PluginAssemblyId, pluginAssembly.PluginAssemblyId) || NameEquals(p.Name, pluginAssembly.Name))) continue;

            exported.RemovePluginAssembly(pluginAssembly.PluginAssemblyId);
            exportedSolution.RemoveRootComponent(ComponentType.PluginAssembly, pluginAssembly.Name, ParseGuid(pluginAssembly.PluginAssemblyId));
            RecordStrip(changes, "plugin assembly", pluginAssembly.Name ?? pluginAssembly.PluginAssemblyId, ComponentType.PluginAssembly, pluginAssembly.PluginAssemblyId);
        }

        foreach (var step in exported.SdkMessageProcessingSteps.ToArray())
        {
            if (MatchesRootComponent(sourceSolution, ComponentType.SdkMessageProcessingStep, step.Name, step.SdkMessageProcessingStepId)) continue;
            if (source.SdkMessageProcessingSteps.Any(s => IdEquals(s.SdkMessageProcessingStepId, step.SdkMessageProcessingStepId))) continue;

            exported.RemoveSdkMessageProcessingStep(step.SdkMessageProcessingStepId);
            exportedSolution.RemoveRootComponent(ComponentType.SdkMessageProcessingStep, step.Name, ParseGuid(step.SdkMessageProcessingStepId));
            RecordStrip(changes, "SDK message processing step", step.Name ?? step.SdkMessageProcessingStepId, ComponentType.SdkMessageProcessingStep, step.SdkMessageProcessingStepId);
        }
    }

    private static void StripEntityWithRelationships(Workspace exported, Workspace source, string logicalName, List<ExportNormalizationChange> changes)
    {
        var danglingRelationships = exported.FindRelationshipsForEntity(logicalName)
            .Where(relationship => !source.Relationships.Any(r => NameEquals(r.SchemaName, relationship.SchemaName)))
            .ToArray();

        exported.RemoveEntity(logicalName);
        RecordStrip(changes, "entity", logicalName, ComponentType.Entity, logicalName);

        foreach (var relationship in danglingRelationships)
        {
            exported.RemoveRelationship(relationship.SchemaName);
            RecordStrip(changes, "relationship", relationship.SchemaName, ComponentType.EntityRelationship, relationship.SchemaName);
        }
    }

    private static void EnforceRootComponentBehavior(Workspace exported, Workspace source, Solution sourceSolution, List<ExportNormalizationChange> changes)
    {
        var entitiesWithoutSubcomponents = sourceSolution.RootComponents
            .Where(rc => rc.Type == ComponentType.Entity
                && rc.BehaviorOption != RootComponentBehavior.IncludeSubcomponents
                && !string.IsNullOrWhiteSpace(rc.SchemaName))
            .Select(rc => rc.SchemaName!)
            .ToArray();

        foreach (var entityName in entitiesWithoutSubcomponents)
        {

            foreach (var form in exported.Forms.Where(f => NameEquals(f.EntityLogicalName, entityName)).ToArray())
            {
                if (MatchesRootComponent(sourceSolution, ComponentType.SystemForm, schemaName: null, form.FormId)) continue;
                if (source.Forms.Any(f => IdEquals(f.FormId, form.FormId))) continue;

                exported.RemoveForm(form.FormId);
                RecordExcludedSubcomponent(changes, "form", form.DisplayName.Default ?? form.FormId, entityName, ComponentType.SystemForm, form.FormId);
            }

            foreach (var view in exported.Views.Where(v => NameEquals(v.EntityLogicalName, entityName)).ToArray())
            {
                if (MatchesRootComponent(sourceSolution, ComponentType.SavedQuery, schemaName: null, view.SavedQueryId)) continue;
                if (source.Views.Any(v => IdEquals(v.SavedQueryId, view.SavedQueryId))) continue;

                exported.RemoveView(view.SavedQueryId);
                RecordExcludedSubcomponent(changes, "view", view.DisplayName.Default ?? view.SavedQueryId, entityName, ComponentType.SavedQuery, view.SavedQueryId);
            }

            if (exported.FindRibbon(entityName) != null && source.FindRibbon(entityName) == null)
            {
                exported.RemoveRibbon(entityName);
                RecordExcludedSubcomponent(changes, "ribbon customization", entityName, entityName, ComponentType.RibbonCustomization, entityName);
            }
        }
    }

    private static void RecordExcludedSubcomponent(List<ExportNormalizationChange> changes, string componentKind, string displayName, string entityName, ComponentType componentType, string target)
    {
        changes.Add(new ExportNormalizationChange(
            ExportNormalizationRule.ExcludedSubcomponent,
            target,
            $"Removed {componentKind} '{displayName}' because entity '{entityName}' excludes subcomponents in the source solution.",
            componentType));
    }

    private static void StripComponentsOwnedByOtherSolutions(Workspace exported, Solution exportedSolution, Workspace source, Solution sourceSolution, List<ExportNormalizationChange> changes)
    {
        foreach (var form in exported.Forms.ToArray())
        {
            var owner = FindForeignOwner(source, sourceSolution, ComponentType.SystemForm, form.FormId);
            if (owner == null) continue;

            exported.RemoveForm(form.FormId);
            exportedSolution.RemoveRootComponent(ComponentType.SystemForm, schemaName: null, ParseGuid(form.FormId));
            changes.Add(new ExportNormalizationChange(
                ExportNormalizationRule.CrossSolutionComponent,
                form.FormId,
                $"Removed form '{form.DisplayName.Default ?? form.FormId}' owned by solution '{owner}'.",
                ComponentType.SystemForm));
        }

        foreach (var view in exported.Views.ToArray())
        {
            var owner = FindForeignOwner(source, sourceSolution, ComponentType.SavedQuery, view.SavedQueryId);
            if (owner == null) continue;

            exported.RemoveView(view.SavedQueryId);
            exportedSolution.RemoveRootComponent(ComponentType.SavedQuery, schemaName: null, ParseGuid(view.SavedQueryId));
            changes.Add(new ExportNormalizationChange(
                ExportNormalizationRule.CrossSolutionComponent,
                view.SavedQueryId,
                $"Removed view '{view.DisplayName.Default ?? view.SavedQueryId}' owned by solution '{owner}'.",
                ComponentType.SavedQuery));
        }
    }

    private static string? FindForeignOwner(Workspace source, Solution sourceSolution, ComponentType type, string objectId)
    {
        var foreignOwner = source.ComponentSourceSnapshots.FirstOrDefault(snapshot =>
            snapshot.Identity.Type == type
            && IdEquals(snapshot.Identity.ObjectId, objectId)
            && !NameEquals(snapshot.SourceSolutionUniqueName, sourceSolution.UniqueName));
        if (foreignOwner == null) return null;

        var ownedByCurrentSolution = source.ComponentSourceSnapshots.Any(snapshot =>
            snapshot.Identity.Type == type
            && IdEquals(snapshot.Identity.ObjectId, objectId)
            && NameEquals(snapshot.SourceSolutionUniqueName, sourceSolution.UniqueName));

        return ownedByCurrentSolution ? null : foreignOwner.SourceSolutionUniqueName;
    }

    private static void StripSystemRelationships(Workspace exported, Workspace source, List<ExportNormalizationChange> changes)
    {
        foreach (var relationship in exported.Relationships.ToArray())
        {
            if (!IsSystemRelationship(relationship)) continue;
            if (source.Relationships.Any(r => NameEquals(r.SchemaName, relationship.SchemaName))) continue;

            exported.RemoveRelationship(relationship.SchemaName);
            changes.Add(new ExportNormalizationChange(
                ExportNormalizationRule.SystemRelationship,
                relationship.SchemaName,
                $"Removed server-added system relationship '{relationship.SchemaName}'.",
                ComponentType.EntityRelationship));
        }
    }

    private static bool IsSystemRelationship(RelationshipMetadata relationship)
    {
        if (relationship is OneToManyRelationshipMetadata oneToMany
            && (SystemRelationshipEntities.Contains(oneToMany.ReferencedEntity ?? string.Empty)
                || SystemRelationshipEntities.Contains(oneToMany.ReferencingEntity ?? string.Empty)))
        {
            return true;
        }

        if (relationship is ManyToManyRelationshipMetadata manyToMany
            && (SystemRelationshipEntities.Contains(manyToMany.Entity1LogicalName ?? string.Empty)
                || SystemRelationshipEntities.Contains(manyToMany.Entity2LogicalName ?? string.Empty)))
        {
            return true;
        }

        return SystemRelationshipNamePattern.IsMatch(relationship.SchemaName ?? string.Empty);
    }

    private static void StripServerVersionAttributes(Workspace exported, Solution exportedSolution, List<ExportNormalizationChange> changes)
    {
        StripRootAttributes(exported, Workspace.GetSolutionDocumentKey(exportedSolution.UniqueName), "Solution.xml", changes);
        StripRootAttributes(exported, $"Generic:{Path.Combine("Other", "Customizations.xml")}", "Customizations.xml", changes);
    }

    private static void StripRootAttributes(Workspace workspace, string documentKey, string displayName, List<ExportNormalizationChange> changes)
    {
        if (!workspace.OriginalDocuments.TryGetValue(documentKey, out var document)) return;

        var root = document.Root;
        if (root == null) return;

        foreach (var attributeName in ServerVersionAttributeNames)
        {
            var attribute = root.Attribute(attributeName);
            if (attribute == null) continue;

            attribute.Remove();
            changes.Add(new ExportNormalizationChange(
                ExportNormalizationRule.ServerVersionAttribute,
                attributeName,
                $"Removed server-enriched attribute '{attributeName}' from {displayName}."));
        }
    }

    private static void NormalizeManagedFlag(Solution exportedSolution, Solution sourceSolution, List<ExportNormalizationChange> changes)
    {
        if (string.Equals(exportedSolution.ManagedValue, sourceSolution.ManagedValue, StringComparison.Ordinal)) return;

        var previous = exportedSolution.ManagedValue;
        exportedSolution.ManagedValue = sourceSolution.ManagedValue;
        changes.Add(new ExportNormalizationChange(
            ExportNormalizationRule.ManagedFlag,
            "Managed",
            $"Normalized Managed flag from '{previous}' to '{sourceSolution.ManagedValue}'."));
    }

    private static void NormalizeSolutionVersion(Solution exportedSolution, Solution sourceSolution, List<ExportNormalizationChange> changes)
    {
        if (string.Equals(exportedSolution.Version, sourceSolution.Version, StringComparison.Ordinal)) return;

        var previous = exportedSolution.Version;
        exportedSolution.Version = sourceSolution.Version;
        changes.Add(new ExportNormalizationChange(
            ExportNormalizationRule.SolutionVersion,
            "Version",
            $"Normalized solution version from '{previous}' to '{sourceSolution.Version}'."));
    }

    private static void RecordStrip(List<ExportNormalizationChange> changes, string componentKind, string displayName, ComponentType componentType, string target)
    {
        changes.Add(new ExportNormalizationChange(
            ExportNormalizationRule.ComponentNotInSource,
            target,
            $"Removed {componentKind} '{displayName}' not present in the source solution.",
            componentType));
    }

    private static bool MatchesRootComponent(Solution solution, ComponentType type, string? schemaName, string? id)
    {
        foreach (var rootComponent in solution.RootComponents)
        {
            if (rootComponent.Type != type) continue;
            if (schemaName != null && NameEquals(rootComponent.SchemaName, schemaName)) return true;
            if (id != null && rootComponent.Id.HasValue && Guid.TryParse(id, out var parsed) && parsed == rootComponent.Id.Value) return true;
        }

        return false;
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : (Guid?)null;

    private static bool NameEquals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IdEquals(string? left, string? right)
    {
        if (NameEquals(left, right)) return true;
        return Guid.TryParse(left, out var leftId) && Guid.TryParse(right, out var rightId) && leftId == rightId;
    }
}
