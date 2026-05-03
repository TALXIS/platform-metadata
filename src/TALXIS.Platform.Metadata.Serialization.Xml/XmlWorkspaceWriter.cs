using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Writes a workspace to disk in SolutionPackager XML format.
/// Roundtrip-safe: if the workspace was loaded via <see cref="XmlWorkspaceReader"/>,
/// original XML elements are patched with known model values, preserving unknown elements and formatting.
/// </summary>
public sealed class XmlWorkspaceWriter
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Writes a workspace to disk in SolutionPackager XML format.
    /// </summary>
    /// <param name="workspace">The workspace to write.</param>
    /// <param name="outputPath">Root directory to write to.</param>
    public void Write(Workspace workspace, string outputPath)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));
        if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

        Directory.CreateDirectory(outputPath);

        if (workspace.Solution != null)
            WriteSolution(workspace.Solution, outputPath, workspace);

        WriteEntities(workspace, outputPath);
        WriteGlobalOptionSets(workspace, outputPath);
        WriteRelationships(workspace, outputPath);
        SaveForms(workspace, outputPath);
        SaveViews(workspace, outputPath);
        SaveWebResources(workspace, outputPath);
        SaveWorkflows(workspace, outputPath);
        SavePluginAssemblies(workspace, outputPath);
        SaveSdkMessageProcessingSteps(workspace, outputPath);
        SaveSecurityRoles(workspace, outputPath);
        SaveAppModules(workspace, outputPath);
        SaveSiteMaps(workspace, outputPath);
        SaveGenericComponents(workspace, outputPath);
    }

    private void WriteSolution(Solution solution, string outputPath, Workspace workspace)
    {
        var otherDir = Path.Combine(outputPath, "Other");
        Directory.CreateDirectory(otherDir);
        var filePath = Path.Combine(otherDir, "Solution.xml");

        var original = workspace.OriginalDocuments.TryGetValue("Solution.xml", out var origDoc)
            ? origDoc
            : null;

        XDocument doc;
        if (original != null)
        {
            doc = new XDocument(original);
            PatchSolution(doc, solution);
        }
        else
        {
            doc = BuildSolutionFromScratch(solution);
        }

        SaveDocument(doc, filePath);
    }

    private static void PatchSolution(XDocument doc, Solution solution)
    {
        var manifest = doc.Root?.Element("SolutionManifest");
        if (manifest == null) return;

        SetElementValue(manifest, "UniqueName", solution.UniqueName);
        SetElementValue(manifest, "Version", solution.Version);
        SetElementValue(manifest, "Managed", solution.ManagedValue);

        // Patch display name
        var localizedNames = manifest.Element("LocalizedNames");
        if (localizedNames != null)
        {
            PatchLocalizedNames(localizedNames, "LocalizedName", solution.DisplayName);
        }

        // Patch publisher
        var pubEl = manifest.Element("Publisher");
        if (pubEl != null && solution.Publisher != null)
        {
            SetElementValue(pubEl, "UniqueName", solution.Publisher.UniqueName);
            SetElementValue(pubEl, "CustomizationPrefix", solution.Publisher.Prefix);
            if (solution.Publisher.OptionValuePrefix.HasValue)
                SetElementValue(pubEl, "CustomizationOptionValuePrefix", solution.Publisher.OptionValuePrefix.Value.ToString());

            var pubNames = pubEl.Element("LocalizedNames");
            if (pubNames != null)
                PatchLocalizedNames(pubNames, "LocalizedName", solution.Publisher.DisplayName);
        }

        // Patch root components
        var rootComponents = manifest.Element("RootComponents");
        if (rootComponents != null)
        {
            ReplaceChildElementsPreservingWhitespace(rootComponents, solution.RootComponents.Select(rc =>
            {
                var rcEl = new XElement("RootComponent",
                    new XAttribute("type", rc.TypeCode.ToString()));
                if (rc.SchemaName != null)
                    rcEl.Add(new XAttribute("schemaName", rc.SchemaName));
                if (rc.Id.HasValue)
                    rcEl.Add(new XAttribute("id", $"{{{rc.Id.Value}}}"));
                rcEl.Add(new XAttribute("behavior", rc.Behavior.ToString()));
                return rcEl;
            }));
        }
    }

    private static XDocument BuildSolutionFromScratch(Solution solution)
    {
        var manifest = new XElement("SolutionManifest",
            new XElement("UniqueName", solution.UniqueName),
            BuildLocalizedNames("LocalizedNames", "LocalizedName", solution.DisplayName),
            new XElement("Descriptions"),
            new XElement("Version", solution.Version),
            new XElement("Managed", solution.ManagedValue));

        if (solution.Publisher != null)
        {
            var pubEl = new XElement("Publisher",
                new XElement("UniqueName", solution.Publisher.UniqueName),
                BuildLocalizedNames("LocalizedNames", "LocalizedName", solution.Publisher.DisplayName),
                new XElement("Descriptions"),
                new XElement("CustomizationPrefix", solution.Publisher.Prefix));
            if (solution.Publisher.OptionValuePrefix.HasValue)
                pubEl.Add(new XElement("CustomizationOptionValuePrefix", solution.Publisher.OptionValuePrefix.Value));
            manifest.Add(pubEl);
        }

        var rootComponents = new XElement("RootComponents");
        foreach (var rc in solution.RootComponents)
        {
            var rcEl = new XElement("RootComponent",
                new XAttribute("type", rc.TypeCode.ToString()));
            if (rc.SchemaName != null)
                rcEl.Add(new XAttribute("schemaName", rc.SchemaName));
            if (rc.Id.HasValue)
                rcEl.Add(new XAttribute("id", $"{{{rc.Id.Value}}}"));
            rcEl.Add(new XAttribute("behavior", rc.Behavior.ToString()));
            rootComponents.Add(rcEl);
        }
        manifest.Add(rootComponents);
        manifest.Add(new XElement("MissingDependencies"));

        var root = new XElement("ImportExportXml",
            new XAttribute("version", "9.1.0.643"),
            new XAttribute("SolutionPackageVersion", "9.1"),
            new XAttribute("languagecode", "1033"),
            new XAttribute("generatedBy", "CrmLive"),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            manifest);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void WriteEntities(Workspace workspace, string outputPath)
    {
        foreach (var entity in workspace.Entities)
        {
            var entityDir = Path.Combine(outputPath, "Entities", entity.LogicalName);
            Directory.CreateDirectory(entityDir);
            var filePath = Path.Combine(entityDir, "Entity.xml");

            var key = $"Entity:{entity.LogicalName}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc)
                ? origDoc
                : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchEntity(doc, entity);
            }
            else
            {
                doc = BuildEntityFromScratch(entity);
            }

            SaveDocument(doc, filePath);
        }
    }

    private static void PatchEntity(XDocument doc, EntityMetadata entity)
    {
        var root = doc.Root;
        if (root == null) return;

        // Patch <Name> element
        var nameEl = root.Element("Name");
        if (nameEl != null)
        {
            nameEl.Value = entity.LogicalName;
            var localizedAttr = nameEl.Attribute("LocalizedName");
            if (localizedAttr != null && entity.DisplayName.Default != null)
                localizedAttr.Value = entity.DisplayName.Default;
            var origAttr = nameEl.Attribute("OriginalName");
            if (origAttr != null && entity.DisplayName.Default != null)
                origAttr.Value = entity.DisplayName.Default;
        }

        var entityInfo = root.Element("EntityInfo")?.Element("entity");
        if (entityInfo == null) return;

        // Patch entity Name attribute
        var entityNameAttr = entityInfo.Attribute("Name");
        if (entityNameAttr != null)
            entityNameAttr.Value = entity.LogicalName;

        // Patch localized names
        var locNames = entityInfo.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", entity.DisplayName);

        var collNames = entityInfo.Element("LocalizedCollectionNames");
        if (collNames != null)
            PatchLocalizedNames(collNames, "LocalizedCollectionName", entity.PluralName);

        // Patch entity-level properties
        SetElementValueIfExists(entityInfo, "EntitySetName", entity.EntitySetName);
        SetElementValueIfExists(entityInfo, "OwnershipTypeMask", entity.Ownership.ToString());
        SetElementValueIfExists(entityInfo, "IsActivity", entity.IsActivity ? "1" : "0");
        SetElementValueIfExists(entityInfo, "IsAuditEnabled", entity.IsAuditEnabled ? "1" : "0");
        SetElementValueIfExists(entityInfo, "ChangeTrackingEnabled", entity.ChangeTrackingEnabled ? "1" : "0");

        // Patch attributes — match by LogicalName, patch known fields
        var attributesEl = entityInfo.Element("attributes");
        if (attributesEl != null)
        {
            foreach (var attrEl in attributesEl.Elements("attribute").ToList())
            {
                var logicalName = attrEl.Element("LogicalName")?.Value;
                if (logicalName == null) continue;

                var modelAttr = entity.FindAttribute(logicalName);
                if (modelAttr == null) continue;

                PatchAttribute(attrEl, modelAttr);
            }
        }
    }

    private static void PatchAttribute(XElement attrEl, AttributeMetadata attr)
    {
        // Patch display name
        var displaynames = attrEl.Element("displaynames");
        if (displaynames != null)
            PatchLocalizedNames(displaynames, "displayname", attr.DisplayName);

        // Patch description
        var descriptions = attrEl.Element("Descriptions");
        if (descriptions != null)
            PatchLocalizedNames(descriptions, "Description", attr.Description);

        SetElementValueIfExists(attrEl, "IsAuditEnabled", attr.IsAuditEnabled ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsSecured", attr.IsSecured ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsSearchable", attr.IsSearchable ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsCustomField", attr.IsCustomAttribute ? "1" : "0");

        SetElementValueIfExists(attrEl, "RequiredLevel", RequiredLevelXml.ToXmlValue(attr.RequiredLevel));
    }

    private static XDocument BuildEntityFromScratch(EntityMetadata entity)
    {
        var displayName = entity.DisplayName.Default ?? entity.LogicalName;

        var entityInfoEl = new XElement("entity",
            new XAttribute("Name", entity.LogicalName));

        entityInfoEl.Add(BuildLocalizedNames("LocalizedNames", "LocalizedName", entity.DisplayName));
        entityInfoEl.Add(BuildLocalizedNames("LocalizedCollectionNames", "LocalizedCollectionName", entity.PluralName));
        entityInfoEl.Add(BuildLocalizedNames("Descriptions", "Description", entity.Description));

        // Attributes
        var attributesEl = new XElement("attributes");
        foreach (var attr in entity.Attributes)
        {
            attributesEl.Add(BuildAttribute(attr));
        }
        entityInfoEl.Add(attributesEl);

        // Entity-level properties
        if (entity.EntitySetName != null)
            entityInfoEl.Add(new XElement("EntitySetName", entity.EntitySetName));
        entityInfoEl.Add(new XElement("OwnershipTypeMask", entity.Ownership.ToString()));
        entityInfoEl.Add(new XElement("IsAuditEnabled", entity.IsAuditEnabled ? "1" : "0"));
        entityInfoEl.Add(new XElement("IsActivity", entity.IsActivity ? "1" : "0"));
        entityInfoEl.Add(new XElement("ChangeTrackingEnabled", entity.ChangeTrackingEnabled ? "1" : "0"));

        var root = new XElement("Entity",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XElement("Name",
                new XAttribute("LocalizedName", displayName),
                new XAttribute("OriginalName", displayName),
                entity.LogicalName),
            new XElement("EntityInfo", entityInfoEl),
            new XElement("FormXml"),
            new XElement("SavedQueries"));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement BuildAttribute(AttributeMetadata attr)
    {
        var physicalName = attr.SchemaName ?? attr.LogicalName;
        var attrEl = new XElement("attribute",
            new XAttribute("PhysicalName", physicalName));

        var typeStr = GetXmlTypeName(attr);
        attrEl.Add(new XElement("Type", typeStr));
        attrEl.Add(new XElement("Name", attr.LogicalName));
        attrEl.Add(new XElement("LogicalName", attr.LogicalName));

        attrEl.Add(new XElement("RequiredLevel", RequiredLevelXml.ToXmlValue(attr.RequiredLevel)));
        attrEl.Add(new XElement("DisplayMask", "ValidForAdvancedFind|ValidForForm|ValidForGrid"));
        attrEl.Add(new XElement("ImeMode", "auto"));
        attrEl.Add(new XElement("ValidForUpdateApi", "1"));
        attrEl.Add(new XElement("ValidForReadApi", "1"));
        attrEl.Add(new XElement("ValidForCreateApi", "1"));
        attrEl.Add(new XElement("IsCustomField", attr.IsCustomAttribute ? "1" : "0"));
        attrEl.Add(new XElement("IsAuditEnabled", attr.IsAuditEnabled ? "1" : "0"));
        attrEl.Add(new XElement("IsSecured", attr.IsSecured ? "1" : "0"));
        attrEl.Add(new XElement("IntroducedVersion", "1.0"));
        attrEl.Add(new XElement("IsCustomizable", "1"));
        attrEl.Add(new XElement("IsRenameable", "1"));
        attrEl.Add(new XElement("CanModifySearchSettings", "1"));
        attrEl.Add(new XElement("CanModifyRequirementLevelSettings", "1"));
        attrEl.Add(new XElement("CanModifyAdditionalSettings", "1"));
        attrEl.Add(new XElement("SourceType", "0"));
        attrEl.Add(new XElement("IsSearchable", attr.IsSearchable ? "1" : "0"));

        // Type-specific elements
        AddTypeSpecificElements(attrEl, attr);

        attrEl.Add(BuildLocalizedNames("displaynames", "displayname", attr.DisplayName));
        attrEl.Add(BuildLocalizedNames("Descriptions", "Description", attr.Description));

        return attrEl;
    }

    private static void AddTypeSpecificElements(XElement attrEl, AttributeMetadata attr)
    {
        switch (attr)
        {
            case StringAttributeMetadata sa:
                attrEl.Add(new XElement("MaxLength", sa.MaxLength));
                if (sa.FormatName != StringFormatName.Text)
                    attrEl.Add(new XElement("Format", sa.FormatName.ToString().ToLowerInvariant()));
                break;
            case MemoAttributeMetadata ma:
                attrEl.Add(new XElement("MaxLength", ma.MaxLength));
                break;
            case IntegerAttributeMetadata ia:
                attrEl.Add(new XElement("MinValue", ia.MinValue));
                attrEl.Add(new XElement("MaxValue", ia.MaxValue));
                if (ia.Format != IntegerFormat.None)
                    attrEl.Add(new XElement("Format", ia.Format.ToString().ToLowerInvariant()));
                break;
            case DecimalAttributeMetadata da:
                attrEl.Add(new XElement("MinValue", da.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("MaxValue", da.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("Precision", da.Precision));
                break;
            case DoubleAttributeMetadata dbl:
                attrEl.Add(new XElement("MinValue", dbl.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("MaxValue", dbl.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("Precision", dbl.Precision));
                break;
            case MoneyAttributeMetadata mo:
                attrEl.Add(new XElement("MinValue", mo.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("MaxValue", mo.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                attrEl.Add(new XElement("Precision", mo.Precision));
                break;
            case DateTimeAttributeMetadata dt:
                var fmt = dt.Format == DateTimeFormat.DateOnly ? "dateonly" : "datetime";
                attrEl.Add(new XElement("Format", fmt));
                var behavior = dt.DateTimeBehavior switch
                {
                    DateTimeBehavior.DateOnly => "1",
                    DateTimeBehavior.TimeZoneIndependent => "2",
                    _ => "0"
                };
                attrEl.Add(new XElement("Behavior", behavior));
                break;
            case LookupAttributeMetadata:
                attrEl.Add(new XElement("LookupStyle", "single"));
                attrEl.Add(new XElement("LookupTypes"));
                break;
        }
    }

    private static string GetXmlTypeName(AttributeMetadata attr)
    {
        return attr.AttributeType switch
        {
            AttributeType.String => "nvarchar",
            AttributeType.Memo => "memo",
            AttributeType.Integer => "int",
            AttributeType.BigInt => "bigint",
            AttributeType.Decimal => "decimal",
            AttributeType.Double => "float",
            AttributeType.Money => "money",
            AttributeType.Boolean => "bit",
            AttributeType.DateTime => "datetime",
            AttributeType.Lookup => "lookup",
            AttributeType.Uniqueidentifier => "uniqueidentifier",
            AttributeType.Picklist => "picklist",
            AttributeType.State => "state",
            AttributeType.Status => "status",
            AttributeType.MultiSelectPicklist => "multiselectpicklist",
            AttributeType.Image => "image",
            AttributeType.File => "file",
            _ => "nvarchar"
        };
    }

    private void WriteGlobalOptionSets(Workspace workspace, string outputPath)
    {
        if (workspace.GlobalOptionSets.Count == 0) return;

        var optionSetsDir = Path.Combine(outputPath, "OptionSets");
        Directory.CreateDirectory(optionSetsDir);

        foreach (var optionSet in workspace.GlobalOptionSets)
        {
            var filePath = Path.Combine(optionSetsDir, $"{optionSet.Name}.xml");

            var key = $"OptionSet:{optionSet.Name}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc)
                ? origDoc
                : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchOptionSet(doc, optionSet);
            }
            else
            {
                doc = BuildOptionSetFromScratch(optionSet);
            }

            SaveDocument(doc, filePath);
        }
    }

    private static void PatchOptionSet(XDocument doc, OptionSetMetadata optionSet)
    {
        var root = doc.Root;
        if (root == null) return;

        var nameAttr = root.Attribute("Name");
        if (nameAttr != null)
            nameAttr.Value = optionSet.Name;

        var localizedNameAttr = root.Attribute("localizedName");
        if (localizedNameAttr != null && optionSet.DisplayName.Default != null)
            localizedNameAttr.Value = optionSet.DisplayName.Default;

        SetElementValueIfExists(root, "IsGlobal", optionSet.IsGlobal ? "1" : "0");

        var displaynames = root.Element("displaynames");
        if (displaynames != null)
            PatchLocalizedNames(displaynames, "displayname", optionSet.DisplayName);

        var descriptions = root.Element("Descriptions");
        if (descriptions != null)
            PatchLocalizedNames(descriptions, "Description", optionSet.Description);

        // Patch options — rebuild to match model
        var optionsEl = root.Element("options");
        if (optionsEl != null)
        {
            // Build a lookup of existing option elements by value for roundtrip
            var existingOptions = new Dictionary<int, XElement>();
            foreach (var optEl in optionsEl.Elements("option").ToList())
            {
                if (int.TryParse(optEl.Attribute("value")?.Value, out var val))
                    existingOptions[val] = optEl;
            }

            ReplaceChildElementsPreservingWhitespace(optionsEl, optionSet.Options.Select(opt =>
            {
                if (existingOptions.TryGetValue(opt.Value, out var existingEl))
                {
                    // Patch the existing element
                    var labels = existingEl.Element("labels");
                    if (labels != null)
                        PatchLocalizedNames(labels, "label", opt.Label);
                    var descs = existingEl.Element("Descriptions");
                    if (descs != null)
                        PatchLocalizedNames(descs, "Description", opt.Description);
                    return existingEl;
                }

                return BuildOptionElement(opt);
            }));
        }
    }

    private static XDocument BuildOptionSetFromScratch(OptionSetMetadata optionSet)
    {
        var root = new XElement("optionset",
            new XAttribute("Name", optionSet.Name),
            new XAttribute("localizedName", optionSet.DisplayName.Default ?? optionSet.Name),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName));

        root.Add(new XElement("OptionSetType", "picklist"));
        root.Add(new XElement("IsGlobal", optionSet.IsGlobal ? "1" : "0"));
        root.Add(new XElement("IntroducedVersion", "1.0"));
        root.Add(new XElement("IsCustomizable", "1"));
        root.Add(new XElement("ExternalTypeName", ""));
        root.Add(BuildLocalizedNames("displaynames", "displayname", optionSet.DisplayName));
        root.Add(BuildLocalizedNames("Descriptions", "Description", optionSet.Description));

        var optionsEl = new XElement("options");
        foreach (var opt in optionSet.Options)
        {
            optionsEl.Add(BuildOptionElement(opt));
        }
        root.Add(optionsEl);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement BuildOptionElement(OptionMetadata opt)
    {
        var optEl = new XElement("option",
            new XAttribute("value", opt.Value),
            new XAttribute("ExternalValue", ""),
            new XAttribute("IsHidden", "0"));

        optEl.Add(BuildLocalizedNames("labels", "label", opt.Label));
        optEl.Add(BuildLocalizedNames("Descriptions", "Description", opt.Description));

        return optEl;
    }

    private void WriteRelationships(Workspace workspace, string outputPath)
    {
        if (workspace.Relationships.Count == 0) return;

        var otherDir = Path.Combine(outputPath, "Other");
        Directory.CreateDirectory(otherDir);
        var filePath = Path.Combine(otherDir, "Relationships.xml");

        var original = workspace.OriginalDocuments.TryGetValue("Relationships.xml", out var origDoc)
            ? origDoc
            : null;

        XDocument doc;
        if (original != null)
        {
            doc = new XDocument(original);
            PatchRelationships(doc, workspace.Relationships);
        }
        else
        {
            doc = BuildRelationshipsFromScratch(workspace.Relationships);
        }

        SaveDocument(doc, filePath);
    }

    private static void PatchRelationships(XDocument doc, IReadOnlyList<RelationshipMetadata> relationships)
    {
        var root = doc.Root;
        if (root == null) return;

        // Update existing elements by Name, preserve unknown child elements
        var existingByName = root.Elements("EntityRelationship")
            .Where(e => !string.IsNullOrEmpty(e.Attribute("Name")?.Value))
            .GroupBy(e => e.Attribute("Name")!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var rel in relationships)
        {
            if (existingByName.TryGetValue(rel.SchemaName, out var existing))
            {
                // Patch known child elements, preserve the rest
                PatchRelationshipChildren(existing, rel);
            }
            else
            {
                root.Add(BuildRelationshipElement(rel));
            }
        }

        // Remove stale relationships not in the model
        var modelNames = new HashSet<string>(relationships.Select(r => r.SchemaName));
        var stale = root.Elements("EntityRelationship")
            .Where(e =>
            {
                var name = e.Attribute("Name")?.Value;
                return !string.IsNullOrEmpty(name) && !modelNames.Contains(name);
            })
            .ToList();
        foreach (var s in stale) s.Remove();
    }

    private static void PatchRelationshipChildren(XElement relEl, RelationshipMetadata rel)
    {
        if (rel is OneToManyRelationshipMetadata oneToMany)
        {
            SetElementValueIfExists(relEl, "ReferencedEntityName", oneToMany.ReferencedEntity);
            SetElementValueIfExists(relEl, "ReferencedAttributeName", oneToMany.ReferencedAttribute);
            SetElementValueIfExists(relEl, "ReferencingEntityName", oneToMany.ReferencingEntity);
            SetElementValueIfExists(relEl, "ReferencingAttributeName", oneToMany.ReferencingAttribute);
        }
        else if (rel is ManyToManyRelationshipMetadata manyToMany)
        {
            SetElementValueIfExists(relEl, "Entity1LogicalName", manyToMany.Entity1LogicalName);
            SetElementValueIfExists(relEl, "Entity2LogicalName", manyToMany.Entity2LogicalName);
            SetElementValueIfExists(relEl, "IntersectEntityName", manyToMany.IntersectEntityName);
        }
    }

    private static XElement BuildRelationshipElement(RelationshipMetadata rel)
    {
        var relEl = new XElement("EntityRelationship",
            new XAttribute("Name", rel.SchemaName));

        if (rel is OneToManyRelationshipMetadata oneToMany)
        {
            if (!string.IsNullOrEmpty(oneToMany.ReferencedEntity))
                relEl.Add(new XElement("ReferencedEntityName", oneToMany.ReferencedEntity));
            if (!string.IsNullOrEmpty(oneToMany.ReferencedAttribute))
                relEl.Add(new XElement("ReferencedAttributeName", oneToMany.ReferencedAttribute));
            if (!string.IsNullOrEmpty(oneToMany.ReferencingEntity))
                relEl.Add(new XElement("ReferencingEntityName", oneToMany.ReferencingEntity));
            if (!string.IsNullOrEmpty(oneToMany.ReferencingAttribute))
                relEl.Add(new XElement("ReferencingAttributeName", oneToMany.ReferencingAttribute));
        }
        else if (rel is ManyToManyRelationshipMetadata manyToMany)
        {
            if (!string.IsNullOrEmpty(manyToMany.Entity1LogicalName))
                relEl.Add(new XElement("Entity1LogicalName", manyToMany.Entity1LogicalName));
            if (!string.IsNullOrEmpty(manyToMany.Entity2LogicalName))
                relEl.Add(new XElement("Entity2LogicalName", manyToMany.Entity2LogicalName));
            if (!string.IsNullOrEmpty(manyToMany.IntersectEntityName))
                relEl.Add(new XElement("IntersectEntityName", manyToMany.IntersectEntityName));
        }

        return relEl;
    }

    private static XDocument BuildRelationshipsFromScratch(IReadOnlyList<RelationshipMetadata> relationships)
    {
        var root = new XElement("EntityRelationships",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName));

        foreach (var rel in relationships)
        {
            root.Add(BuildRelationshipElement(rel));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    // --- New component types ---

    private void SaveForms(Workspace workspace, string outputPath)
    {
        foreach (var form in workspace.Forms)
        {
            var key = $"Form:{form.EntityLogicalName}:{form.FormId}";
            if (!workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
                continue; // Forms have complex XML bodies — skip if no original

            var doc = new XDocument(origDoc);
            PatchForm(doc, form);

            var formType = form.FormType ?? "main";
            var entityDir = Path.Combine(outputPath, "Entities", form.EntityLogicalName ?? "Unknown", "FormXml", formType);
            Directory.CreateDirectory(entityDir);
            var fileName = form.FormId.StartsWith("{") ? $"{form.FormId}.xml" : $"{{{form.FormId}}}.xml";
            var filePath = Path.Combine(entityDir, fileName);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchForm(XDocument doc, FormMetadata form)
    {
        var systemForm = doc.Root?.Element("systemform");
        if (systemForm == null) return;

        SetElementValueIfExists(systemForm, "formid", form.FormId);
        if (form.IntroducedVersion != null)
            SetElementValueIfExists(systemForm, "IntroducedVersion", form.IntroducedVersion);
        if (form.FormPresentation.HasValue)
            SetElementValueIfExists(systemForm, "FormPresentation", form.FormPresentation.Value.ToString());
        if (form.FormActivationState.HasValue)
            SetElementValueIfExists(systemForm, "FormActivationState", form.FormActivationState.Value.ToString());

        var locNames = systemForm.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", form.DisplayName);

        var descriptions = systemForm.Element("Descriptions");
        if (descriptions != null)
            PatchLocalizedNames(descriptions, "Description", form.Description);
    }

    private void SaveViews(Workspace workspace, string outputPath)
    {
        foreach (var view in workspace.Views)
        {
            var key = $"View:{view.EntityLogicalName}:{view.SavedQueryId}";
            if (!workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
                continue; // Views have complex XML bodies — skip if no original

            var doc = new XDocument(origDoc);
            PatchView(doc, view);

            var entityDir = Path.Combine(outputPath, "Entities", view.EntityLogicalName ?? "Unknown", "SavedQueries");
            Directory.CreateDirectory(entityDir);
            var fileName = view.SavedQueryId.StartsWith("{") ? $"{view.SavedQueryId}.xml" : $"{{{view.SavedQueryId}}}.xml";
            var filePath = Path.Combine(entityDir, fileName);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchView(XDocument doc, SavedQueryMetadata view)
    {
        var savedQuery = doc.Root?.Element("savedquery");
        if (savedQuery == null) return;

        SetElementValueIfExists(savedQuery, "savedqueryid", view.SavedQueryId);
        if (view.IntroducedVersion != null)
            SetElementValueIfExists(savedQuery, "IntroducedVersion", view.IntroducedVersion);
        SetElementValueIfExists(savedQuery, "isdefault", view.IsDefault ? "1" : "0");
        if (view.QueryType.HasValue)
            SetElementValueIfExists(savedQuery, "querytype", view.QueryType.Value.ToString());
        if (view.FetchXml != null)
            SetElementContentPreserveCData(savedQuery, "fetchxml", view.FetchXml);
        if (view.LayoutXml != null)
            SetElementContentPreserveCData(savedQuery, "layoutxml", view.LayoutXml);

        var locNames = savedQuery.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", view.Name);

        var descriptions = savedQuery.Element("Descriptions");
        if (descriptions != null)
            PatchLocalizedNames(descriptions, "Description", view.Description);
    }

    private void SaveWebResources(Workspace workspace, string outputPath)
    {
        foreach (var webResource in workspace.WebResources)
        {
            var key = $"WebResource:{webResource.Name}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc) ? origDoc : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchWebResource(doc, webResource);
            }
            else
            {
                doc = BuildWebResourceFromScratch(webResource);
            }

            var webResourcesDir = Path.Combine(outputPath, "WebResources");
            Directory.CreateDirectory(webResourcesDir);
            // Use the Name with slashes replaced for file path, keeping .data.xml extension
            var safeName = webResource.Name.Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(webResourcesDir, safeName + ".data.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchWebResource(XDocument doc, WebResourceMetadata webResource)
    {
        var root = doc.Root;
        if (root == null) return;

        SetElementValueIfExists(root, "WebResourceId", webResource.WebResourceId);
        SetElementValueIfExists(root, "Name", webResource.Name);
        if (webResource.DisplayName != null)
            SetElementValueIfExists(root, "DisplayName", webResource.DisplayName);
        SetElementValueIfExists(root, "WebResourceType", webResource.WebResourceType.ToString());
        if (webResource.FileName != null)
            SetElementValueIfExists(root, "FileName", webResource.FileName);
        SetElementValueIfExists(root, "IsCustomizable", webResource.IsCustomizable ? "1" : "0");
        SetElementValueIfExists(root, "CanBeDeleted", webResource.CanBeDeleted ? "1" : "0");
        SetElementValueIfExists(root, "IsHidden", webResource.IsHidden ? "1" : "0");
        SetElementValueIfExists(root, "IsEnabledForMobileClient", webResource.IsEnabledForMobileClient ? "1" : "0");
        SetElementValueIfExists(root, "IsAvailableForMobileOffline", webResource.IsAvailableForMobileOffline ? "1" : "0");
    }

    private static XDocument BuildWebResourceFromScratch(WebResourceMetadata webResource)
    {
        var root = new XElement("WebResource",
            new XElement("WebResourceId", webResource.WebResourceId),
            new XElement("Name", webResource.Name),
            new XElement("WebResourceType", webResource.WebResourceType));

        if (webResource.DisplayName != null)
            root.Add(new XElement("DisplayName", webResource.DisplayName));
        if (webResource.FileName != null)
            root.Add(new XElement("FileName", webResource.FileName));
        if (webResource.IntroducedVersion != null)
            root.Add(new XElement("IntroducedVersion", webResource.IntroducedVersion));
        root.Add(new XElement("IsCustomizable", webResource.IsCustomizable ? "1" : "0"));
        root.Add(new XElement("CanBeDeleted", webResource.CanBeDeleted ? "1" : "0"));
        root.Add(new XElement("IsHidden", webResource.IsHidden ? "1" : "0"));
        root.Add(new XElement("IsEnabledForMobileClient", webResource.IsEnabledForMobileClient ? "1" : "0"));
        root.Add(new XElement("IsAvailableForMobileOffline", webResource.IsAvailableForMobileOffline ? "1" : "0"));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void SaveWorkflows(Workspace workspace, string outputPath)
    {
        foreach (var workflow in workspace.Workflows)
        {
            var key = $"Workflow:{workflow.WorkflowId}";
            if (!workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
                continue; // Workflows have complex XAML bodies — skip if no original

            var doc = new XDocument(origDoc);
            PatchWorkflow(doc, workflow);

            // Preserve original file path when available for roundtrip fidelity
            var filePath = TryGetOriginalRelativePath(workflow.Source, workspace.RootPath, outputPath);
            if (filePath == null)
            {
                var workflowsDir = Path.Combine(outputPath, "Workflows");
                Directory.CreateDirectory(workflowsDir);
                var fileName = workflow.UniqueName ?? workflow.WorkflowId;
                filePath = Path.Combine(workflowsDir, $"{fileName}.data.xml");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchWorkflow(XDocument doc, WorkflowMetadata workflow)
    {
        var root = doc.Root;
        if (root == null) return;

        // Patch root attributes
        var workflowIdAttr = root.Attribute("WorkflowId");
        if (workflowIdAttr != null) workflowIdAttr.Value = workflow.WorkflowId;

        // Patch child elements
        if (workflow.UniqueName != null)
            SetElementValueIfExists(root, "UniqueName", workflow.UniqueName);
        if (workflow.Category.HasValue)
            SetElementValueIfExists(root, "Category", workflow.Category.Value.ToString());
        if (workflow.Type.HasValue)
            SetElementValueIfExists(root, "Type", workflow.Type.Value.ToString());
        if (workflow.Mode.HasValue)
            SetElementValueIfExists(root, "Mode", workflow.Mode.Value.ToString());
        if (workflow.Scope.HasValue)
            SetElementValueIfExists(root, "Scope", workflow.Scope.Value.ToString());
        if (workflow.PrimaryEntity != null)
            SetElementValueIfExists(root, "PrimaryEntity", workflow.PrimaryEntity);
        SetElementValueIfExists(root, "IsCustomizable", workflow.IsCustomizable ? "1" : "0");
        SetElementValueIfExists(root, "TriggerOnCreate", workflow.TriggerOnCreate ? "1" : "0");
        SetElementValueIfExists(root, "TriggerOnDelete", workflow.TriggerOnDelete ? "1" : "0");
        SetElementValueIfExists(root, "OnDemand", workflow.OnDemand ? "1" : "0");

        var locNames = root.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", workflow.Name);

        var descriptions = root.Element("Descriptions");
        if (descriptions != null)
            PatchLocalizedNames(descriptions, "Description", workflow.Description);
    }

    private void SavePluginAssemblies(Workspace workspace, string outputPath)
    {
        foreach (var assembly in workspace.PluginAssemblies)
        {
            var key = $"PluginAssembly:{assembly.Name}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc) ? origDoc : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchPluginAssembly(doc, assembly);
            }
            else
            {
                doc = BuildPluginAssemblyFromScratch(assembly);
            }

            // Preserve original file path when available for roundtrip fidelity
            var filePath = TryGetOriginalRelativePath(assembly.Source, workspace.RootPath, outputPath);
            if (filePath == null)
            {
                var assemblyName = assembly.Name ?? assembly.PluginAssemblyId;
                var assemblyDir = Path.Combine(outputPath, "PluginAssemblies", assemblyName);
                Directory.CreateDirectory(assemblyDir);
                filePath = Path.Combine(assemblyDir, $"{assemblyName}.data.xml");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchPluginAssembly(XDocument doc, PluginAssemblyMetadata assembly)
    {
        var root = doc.Root;
        if (root == null) return;

        var idAttr = root.Attribute("PluginAssemblyId");
        if (idAttr != null) idAttr.Value = assembly.PluginAssemblyId;

        var fullNameAttr = root.Attribute("FullName");
        if (fullNameAttr != null && assembly.FullName != null)
            fullNameAttr.Value = assembly.FullName;

        if (assembly.IsolationMode.HasValue)
            SetElementValueIfExists(root, "IsolationMode", assembly.IsolationMode.Value.ToString());
        if (assembly.SourceType.HasValue)
            SetElementValueIfExists(root, "SourceType", assembly.SourceType.Value.ToString());
        if (assembly.FileName != null)
            SetElementValueIfExists(root, "FileName", assembly.FileName);
        if (assembly.CustomizationLevel.HasValue)
            SetElementValueIfExists(root, "CustomizationLevel", assembly.CustomizationLevel.Value.ToString());

        // Patch plugin types
        var pluginTypesEl = root.Element("PluginTypes");
        if (pluginTypesEl != null)
        {
            foreach (var ptEl in pluginTypesEl.Elements("PluginType").ToList())
            {
                var ptId = ptEl.Attribute("PluginTypeId")?.Value;
                if (ptId == null) continue;

                var modelPt = assembly.PluginTypes.FirstOrDefault(pt => pt.PluginTypeId == ptId);
                if (modelPt == null) continue;

                if (modelPt.Name != null)
                {
                    var nameAttr = ptEl.Attribute("Name");
                    if (nameAttr != null) nameAttr.Value = modelPt.Name;
                }
                if (modelPt.FriendlyName != null)
                    SetElementValueIfExists(ptEl, "FriendlyName", modelPt.FriendlyName);
                if (modelPt.TypeName != null)
                    SetElementValueIfExists(ptEl, "TypeName", modelPt.TypeName);
            }
        }
    }

    private static XDocument BuildPluginAssemblyFromScratch(PluginAssemblyMetadata assembly)
    {
        var root = new XElement("PluginAssembly",
            new XAttribute("PluginAssemblyId", assembly.PluginAssemblyId));

        if (assembly.FullName != null)
            root.Add(new XAttribute("FullName", assembly.FullName));

        if (assembly.IsolationMode.HasValue)
            root.Add(new XElement("IsolationMode", assembly.IsolationMode.Value));
        if (assembly.SourceType.HasValue)
            root.Add(new XElement("SourceType", assembly.SourceType.Value));
        if (assembly.FileName != null)
            root.Add(new XElement("FileName", assembly.FileName));
        if (assembly.IntroducedVersion != null)
            root.Add(new XElement("IntroducedVersion", assembly.IntroducedVersion));
        if (assembly.CustomizationLevel.HasValue)
            root.Add(new XElement("CustomizationLevel", assembly.CustomizationLevel.Value));

        if (assembly.PluginTypes.Count > 0)
        {
            var pluginTypesEl = new XElement("PluginTypes");
            foreach (var pt in assembly.PluginTypes)
            {
                var ptEl = new XElement("PluginType",
                    new XAttribute("PluginTypeId", pt.PluginTypeId));
                if (pt.Name != null)
                    ptEl.Add(new XAttribute("Name", pt.Name));
                if (pt.AssemblyQualifiedName != null)
                    ptEl.Add(new XAttribute("AssemblyQualifiedName", pt.AssemblyQualifiedName));
                if (pt.FriendlyName != null)
                    ptEl.Add(new XElement("FriendlyName", pt.FriendlyName));
                if (pt.TypeName != null)
                    ptEl.Add(new XElement("TypeName", pt.TypeName));
                if (pt.WorkflowActivityGroupName != null)
                    ptEl.Add(new XElement("WorkflowActivityGroupName", pt.WorkflowActivityGroupName));
                pluginTypesEl.Add(ptEl);
            }
            root.Add(pluginTypesEl);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void SaveSdkMessageProcessingSteps(Workspace workspace, string outputPath)
    {
        foreach (var step in workspace.SdkMessageProcessingSteps)
        {
            var key = $"Step:{step.SdkMessageProcessingStepId}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc) ? origDoc : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchSdkMessageProcessingStep(doc, step);
            }
            else
            {
                doc = BuildSdkMessageProcessingStepFromScratch(step);
            }

            var stepsDir = Path.Combine(outputPath, "SdkMessageProcessingSteps");
            Directory.CreateDirectory(stepsDir);
            var fileName = step.SdkMessageProcessingStepId.StartsWith("{")
                ? $"{step.SdkMessageProcessingStepId}.xml"
                : $"{{{step.SdkMessageProcessingStepId}}}.xml";
            var filePath = Path.Combine(stepsDir, fileName);
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchSdkMessageProcessingStep(XDocument doc, SdkMessageProcessingStepMetadata step)
    {
        var root = doc.Root;
        if (root == null) return;

        var idAttr = root.Attribute("SdkMessageProcessingStepId");
        if (idAttr != null) idAttr.Value = step.SdkMessageProcessingStepId;

        if (step.Name != null)
        {
            var nameAttr = root.Attribute("Name");
            if (nameAttr != null) nameAttr.Value = step.Name;
        }

        if (step.SdkMessageId != null)
            SetElementValueIfExists(root, "SdkMessageId", step.SdkMessageId);
        if (step.PluginTypeName != null)
            SetElementValueIfExists(root, "PluginTypeName", step.PluginTypeName);
        if (step.PluginTypeId != null)
            SetElementValueIfExists(root, "PluginTypeId", step.PluginTypeId);
        if (step.Stage.HasValue)
            SetElementValueIfExists(root, "Stage", step.Stage.Value.ToString());
        if (step.Mode.HasValue)
            SetElementValueIfExists(root, "Mode", step.Mode.Value.ToString());
        if (step.Rank.HasValue)
            SetElementValueIfExists(root, "Rank", step.Rank.Value.ToString());
        if (step.FilteringAttributes != null)
            SetElementValueIfExists(root, "FilteringAttributes", step.FilteringAttributes);
        SetElementValueIfExists(root, "AsyncAutoDelete", step.AsyncAutoDelete ? "1" : "0");
        if (step.Description != null)
            SetElementValueIfExists(root, "Description", step.Description);
        SetElementValueIfExists(root, "IsCustomizable", step.IsCustomizable ? "1" : "0");
        SetElementValueIfExists(root, "IsHidden", step.IsHidden ? "1" : "0");

        // Patch images
        var imagesEl = root.Element("SdkMessageProcessingStepImages");
        if (imagesEl != null)
        {
            foreach (var imgEl in imagesEl.Elements("SdkMessageProcessingStepImage").ToList())
            {
                var imgId = imgEl.Attribute("SdkMessageProcessingStepImageId")?.Value;
                if (imgId == null) continue;

                var modelImg = step.Images.FirstOrDefault(i => i.SdkMessageProcessingStepImageId == imgId);
                if (modelImg == null) continue;

                if (modelImg.ImageType.HasValue)
                    SetElementValueIfExists(imgEl, "ImageType", modelImg.ImageType.Value.ToString());
                if (modelImg.MessagePropertyName != null)
                    SetElementValueIfExists(imgEl, "MessagePropertyName", modelImg.MessagePropertyName);
                if (modelImg.EntityAlias != null)
                    SetElementValueIfExists(imgEl, "EntityAlias", modelImg.EntityAlias);
                if (modelImg.Attributes != null)
                    SetElementValueIfExists(imgEl, "Attributes", modelImg.Attributes);
            }
        }
    }

    private static XDocument BuildSdkMessageProcessingStepFromScratch(SdkMessageProcessingStepMetadata step)
    {
        var root = new XElement("SdkMessageProcessingStep",
            new XAttribute("SdkMessageProcessingStepId", step.SdkMessageProcessingStepId));

        if (step.Name != null)
            root.Add(new XAttribute("Name", step.Name));

        if (step.SdkMessageId != null)
            root.Add(new XElement("SdkMessageId", step.SdkMessageId));
        if (step.PluginTypeName != null)
            root.Add(new XElement("PluginTypeName", step.PluginTypeName));
        if (step.PluginTypeId != null)
            root.Add(new XElement("PluginTypeId", step.PluginTypeId));
        if (step.Stage.HasValue)
            root.Add(new XElement("Stage", step.Stage.Value));
        if (step.Mode.HasValue)
            root.Add(new XElement("Mode", step.Mode.Value));
        if (step.Rank.HasValue)
            root.Add(new XElement("Rank", step.Rank.Value));
        if (step.FilteringAttributes != null)
            root.Add(new XElement("FilteringAttributes", step.FilteringAttributes));
        root.Add(new XElement("AsyncAutoDelete", step.AsyncAutoDelete ? "1" : "0"));
        if (step.Description != null)
            root.Add(new XElement("Description", step.Description));
        if (step.SupportedDeployment.HasValue)
            root.Add(new XElement("SupportedDeployment", step.SupportedDeployment.Value));
        if (step.InvocationSource.HasValue)
            root.Add(new XElement("InvocationSource", step.InvocationSource.Value));
        if (step.EventHandlerTypeCode.HasValue)
            root.Add(new XElement("EventHandlerTypeCode", step.EventHandlerTypeCode.Value));
        if (step.IntroducedVersion != null)
            root.Add(new XElement("IntroducedVersion", step.IntroducedVersion));
        root.Add(new XElement("IsCustomizable", step.IsCustomizable ? "1" : "0"));
        root.Add(new XElement("IsHidden", step.IsHidden ? "1" : "0"));

        if (step.Images.Count > 0)
        {
            var imagesEl = new XElement("SdkMessageProcessingStepImages");
            foreach (var img in step.Images)
            {
                var imgEl = new XElement("SdkMessageProcessingStepImage",
                    new XAttribute("SdkMessageProcessingStepImageId", img.SdkMessageProcessingStepImageId));
                if (img.ImageType.HasValue)
                    imgEl.Add(new XElement("ImageType", img.ImageType.Value));
                if (img.MessagePropertyName != null)
                    imgEl.Add(new XElement("MessagePropertyName", img.MessagePropertyName));
                if (img.EntityAlias != null)
                    imgEl.Add(new XElement("EntityAlias", img.EntityAlias));
                if (img.Attributes != null)
                    imgEl.Add(new XElement("Attributes", img.Attributes));
                imagesEl.Add(imgEl);
            }
            root.Add(imagesEl);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void SaveSecurityRoles(Workspace workspace, string outputPath)
    {
        foreach (var role in workspace.SecurityRoles)
        {
            var key = $"Role:{role.RoleId}";
            var original = workspace.OriginalDocuments.TryGetValue(key, out var origDoc) ? origDoc : null;

            XDocument doc;
            if (original != null)
            {
                doc = new XDocument(original);
                PatchSecurityRole(doc, role);
            }
            else
            {
                doc = BuildSecurityRoleFromScratch(role);
            }

            var rolesDir = Path.Combine(outputPath, "Roles");
            Directory.CreateDirectory(rolesDir);
            var filePath = Path.Combine(rolesDir, $"{role.Name}.xml");
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchSecurityRole(XDocument doc, SecurityRoleMetadata role)
    {
        var root = doc.Root;
        if (root == null) return;

        var idAttr = root.Attribute("id");
        if (idAttr != null) idAttr.Value = role.RoleId;

        var nameAttr = root.Attribute("name");
        if (nameAttr != null) nameAttr.Value = role.Name;

        var inheritedAttr = root.Attribute("isinherited");
        if (inheritedAttr != null) inheritedAttr.Value = role.IsInherited ? "1" : "0";

        if (role.IntroducedVersion != null)
            SetElementValueIfExists(root, "IntroducedVersion", role.IntroducedVersion);

        // Rebuild privileges from model
        var privilegesEl = root.Element("RolePrivileges");
        if (privilegesEl != null)
        {
            ReplaceChildElementsPreservingWhitespace(privilegesEl, role.Privileges.Select(priv =>
                new XElement("RolePrivilege",
                    new XAttribute("name", priv.Name),
                    new XAttribute("level", priv.Level))));
        }
    }

    private static XDocument BuildSecurityRoleFromScratch(SecurityRoleMetadata role)
    {
        var root = new XElement("Role",
            new XAttribute("id", role.RoleId),
            new XAttribute("name", role.Name),
            new XAttribute("isinherited", role.IsInherited ? "1" : "0"));

        if (role.IntroducedVersion != null)
            root.Add(new XElement("IntroducedVersion", role.IntroducedVersion));

        var privilegesEl = new XElement("RolePrivileges");
        foreach (var priv in role.Privileges)
        {
            privilegesEl.Add(new XElement("RolePrivilege",
                new XAttribute("name", priv.Name),
                new XAttribute("level", priv.Level)));
        }
        root.Add(privilegesEl);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void SaveAppModules(Workspace workspace, string outputPath)
    {
        foreach (var appModule in workspace.AppModules)
        {
            var key = $"AppModule:{appModule.UniqueName}";
            if (!workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
                continue; // AppModules have complex XML bodies — skip if no original

            var doc = new XDocument(origDoc);
            PatchAppModule(doc, appModule);

            var appModuleDir = Path.Combine(outputPath, "AppModules", appModule.UniqueName);
            Directory.CreateDirectory(appModuleDir);
            var filePath = Path.Combine(appModuleDir, "AppModule.xml");
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchAppModule(XDocument doc, AppModuleMetadata appModule)
    {
        var root = doc.Root;
        if (root == null) return;

        SetElementValueIfExists(root, "UniqueName", appModule.UniqueName);
        if (appModule.IntroducedVersion != null)
            SetElementValueIfExists(root, "IntroducedVersion", appModule.IntroducedVersion);
        if (appModule.WebResourceId != null)
            SetElementValueIfExists(root, "WebResourceId", appModule.WebResourceId);
        if (appModule.FormFactor.HasValue)
            SetElementValueIfExists(root, "FormFactor", appModule.FormFactor.Value.ToString());
        if (appModule.ClientType.HasValue)
            SetElementValueIfExists(root, "ClientType", appModule.ClientType.Value.ToString());
        if (appModule.NavigationType.HasValue)
            SetElementValueIfExists(root, "NavigationType", appModule.NavigationType.Value.ToString());

        var locNames = root.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", appModule.DisplayName);

        // Patch components
        var componentsEl = root.Element("AppModuleComponents");
        if (componentsEl != null)
        {
            ReplaceChildElementsPreservingWhitespace(componentsEl, appModule.Components.Select(comp =>
            {
                var compEl = new XElement("AppModuleComponent",
                    new XAttribute("type", comp.Type.ToString()));
                if (comp.SchemaName != null)
                    compEl.Add(new XAttribute("schemaName", comp.SchemaName));
                if (comp.Id != null)
                    compEl.Add(new XAttribute("id", comp.Id));
                return compEl;
            }));
        }

        // Patch role maps
        var roleMapsEl = root.Element("AppModuleRoleMaps");
        if (roleMapsEl != null)
        {
            ReplaceChildElementsPreservingWhitespace(roleMapsEl, appModule.RoleIds.Select(roleId =>
                new XElement("Role",
                    new XAttribute("id", roleId))));
        }
    }

    private void SaveSiteMaps(Workspace workspace, string outputPath)
    {
        foreach (var siteMap in workspace.SiteMaps)
        {
            var key = $"SiteMap:{siteMap.UniqueName}";
            if (!workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
                continue; // SiteMaps have complex XML bodies — skip if no original

            var doc = new XDocument(origDoc);
            PatchSiteMap(doc, siteMap);

            var siteMapDir = Path.Combine(outputPath, "AppModuleSiteMaps", siteMap.UniqueName);
            Directory.CreateDirectory(siteMapDir);
            var filePath = Path.Combine(siteMapDir, $"{siteMap.UniqueName}.xml");
            SaveDocument(doc, filePath);
        }
    }

    private static void PatchSiteMap(XDocument doc, SiteMapMetadata siteMap)
    {
        var root = doc.Root;
        if (root == null) return;

        SetElementValueIfExists(root, "SiteMapUniqueName", siteMap.UniqueName);
        SetElementValueIfExists(root, "EnableCollapsibleGroups", siteMap.EnableCollapsibleGroups ? "True" : "False");
        SetElementValueIfExists(root, "ShowHome", siteMap.ShowHome ? "True" : "False");
        SetElementValueIfExists(root, "ShowPinned", siteMap.ShowPinned ? "True" : "False");
        SetElementValueIfExists(root, "ShowRecents", siteMap.ShowRecents ? "True" : "False");

        var locNames = root.Element("LocalizedNames");
        if (locNames != null)
            PatchLocalizedNames(locNames, "LocalizedName", siteMap.DisplayName);
    }

    // --- Helpers ---

    private static void SetElementValue(XElement parent, string elementName, string value)
    {
        var el = parent.Element(elementName);
        if (el != null)
            el.Value = value;
        else
            parent.Add(new XElement(elementName, value));
    }

    /// <summary>
    /// Updates an element's content, preserving CDATA wrapping if the original content used it.
    /// </summary>
    private static void SetElementContentPreserveCData(XElement parent, string elementName, string? value)
    {
        if (value == null) return;
        var el = parent.Element(elementName);
        if (el == null) return;

        var existingCdata = el.Nodes().OfType<XCData>().FirstOrDefault();
        if (existingCdata != null)
        {
            if (existingCdata.Value != value)
                existingCdata.Value = value;
        }
        else
        {
            if (el.Value != value)
                el.Value = value;
        }
    }

    private static void SetElementValueIfExists(XElement parent, string elementName, string? value)
    {
        if (value == null) return;
        var el = parent.Element(elementName);
        if (el != null)
            el.Value = value;
    }

    private static void PatchLocalizedNames(XElement container, string childName, Label label)
    {
        foreach (var kvp in label.LocalizedLabels)
        {
            var existing = container.Elements(childName)
                .FirstOrDefault(e => e.Attribute("languagecode")?.Value == kvp.Key.ToString());
            if (existing != null)
            {
                var descAttr = existing.Attribute("description");
                if (descAttr != null)
                    descAttr.Value = kvp.Value;
            }
            else
            {
                container.Add(new XElement(childName,
                    new XAttribute("description", kvp.Value),
                    new XAttribute("languagecode", kvp.Key)));
            }
        }
    }

    private static XElement BuildLocalizedNames(string containerName, string childName, Label label)
    {
        var container = new XElement(containerName);
        foreach (var kvp in label.LocalizedLabels)
        {
            container.Add(new XElement(childName,
                new XAttribute("description", kvp.Value),
                new XAttribute("languagecode", kvp.Key)));
        }
        return container;
    }

    private void SaveGenericComponents(Workspace workspace, string outputPath)
    {
        foreach (var component in workspace.GenericComponents)
        {
            if (component.FilePath == null) continue;

            var key = $"Generic:{component.FilePath}";
            XDocument doc;

            if (workspace.OriginalDocuments.TryGetValue(key, out var origDoc))
            {
                doc = new XDocument(origDoc);
            }
            else if (component.SerializedContent != null)
            {
                try
                {
                    doc = XDocument.Parse(component.SerializedContent);
                }
                catch (System.Xml.XmlException ex)
                {
                    throw new InvalidOperationException(
                        $"Generic component '{component.ComponentTypeName}' at '{component.FilePath}' has malformed serialized content: {ex.Message}", ex);
                }
            }
            else
            {
                continue;
            }

            var filePath = Path.Combine(outputPath, component.FilePath);
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            SaveDocument(doc, filePath);
        }
    }

    /// <summary>
    /// If the metadata was loaded from a file under <paramref name="originalRoot"/>,
    /// returns the equivalent path under <paramref name="outputRoot"/>; otherwise null.
    /// </summary>
    private static string? TryGetOriginalRelativePath(SourceLocation? source, string originalRoot, string outputRoot)
    {
        if (source?.FilePath == null) return null;

        var normalizedRoot = originalRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedFile = source.FilePath;

        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        var relativePath = normalizedFile.Substring(normalizedRoot.Length);
        return Path.Combine(outputRoot, relativePath);
    }

    private static void SaveDocument(XDocument doc, string filePath)
    {
        if (HasPreservedWhitespace(doc))
        {
            using var stream = File.Create(filePath);
            using var textWriter = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            doc.Save(textWriter, SaveOptions.DisableFormatting);
            return;
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new System.Text.UTF8Encoding(false),
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(filePath, settings);
        doc.Save(writer);
    }

    private static bool HasPreservedWhitespace(XDocument doc)
    {
        return (doc.Root?.DescendantNodesAndSelf() ?? Enumerable.Empty<XNode>())
            .OfType<XText>()
            .Any(static text => string.IsNullOrWhiteSpace(text.Value));
    }

    private static void ReplaceChildElementsPreservingWhitespace(XElement parent, IEnumerable<XElement> children)
    {
        var replacements = children.ToList();
        var childIndent = parent.Nodes()
            .OfType<XText>()
            .Select(text => text.Value)
            .FirstOrDefault(ContainsNewLine);
        var closingIndent = parent.Nodes()
            .OfType<XText>()
            .Select(text => text.Value)
            .LastOrDefault(ContainsNewLine);

        parent.RemoveNodes();

        if (childIndent == null || closingIndent == null || replacements.Count == 0)
        {
            foreach (var child in replacements)
            {
                parent.Add(child);
            }

            return;
        }

        foreach (var child in replacements)
        {
            parent.Add(new XText(childIndent));
            parent.Add(child);
        }

        parent.Add(new XText(closingIndent));
    }

    private static bool ContainsNewLine(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }
}
