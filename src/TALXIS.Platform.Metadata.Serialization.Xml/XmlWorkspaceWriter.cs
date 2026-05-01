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
        SetElementValue(manifest, "Managed", solution.IsManaged ? "2" : "2");

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
            rootComponents.RemoveAll();
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
        }
    }

    private static XDocument BuildSolutionFromScratch(Solution solution)
    {
        var manifest = new XElement("SolutionManifest",
            new XElement("UniqueName", solution.UniqueName),
            BuildLocalizedNames("LocalizedNames", "LocalizedName", solution.DisplayName),
            new XElement("Descriptions"),
            new XElement("Version", solution.Version),
            new XElement("Managed", solution.IsManaged ? "1" : "0"));

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
            PatchDescriptions(descriptions, attr.Description);

        SetElementValueIfExists(attrEl, "IsAuditEnabled", attr.IsAuditEnabled ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsSecured", attr.IsSecured ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsSearchable", attr.IsSearchable ? "1" : "0");
        SetElementValueIfExists(attrEl, "IsCustomField", attr.IsCustomAttribute ? "1" : "0");

        var reqLevel = attr.RequiredLevel switch
        {
            RequiredLevel.Required => "required",
            RequiredLevel.Recommended => "recommended",
            _ => "none"
        };
        SetElementValueIfExists(attrEl, "RequiredLevel", reqLevel);
    }

    private static XDocument BuildEntityFromScratch(EntityMetadata entity)
    {
        var displayName = entity.DisplayName.Default ?? entity.LogicalName;

        var entityInfoEl = new XElement("entity",
            new XAttribute("Name", entity.LogicalName));

        entityInfoEl.Add(BuildLocalizedNames("LocalizedNames", "LocalizedName", entity.DisplayName));
        entityInfoEl.Add(BuildLocalizedNames("LocalizedCollectionNames", "LocalizedCollectionName", entity.PluralName));
        entityInfoEl.Add(BuildDescriptions(entity.Description));

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

        var reqLevel = attr.RequiredLevel switch
        {
            RequiredLevel.Required => "required",
            RequiredLevel.Recommended => "recommended",
            _ => "none"
        };
        attrEl.Add(new XElement("RequiredLevel", reqLevel));
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
        attrEl.Add(BuildDescriptions(attr.Description));

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
            PatchDescriptions(descriptions, optionSet.Description);

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

            optionsEl.RemoveAll();
            foreach (var opt in optionSet.Options)
            {
                if (existingOptions.TryGetValue(opt.Value, out var existingEl))
                {
                    // Patch the existing element
                    var labels = existingEl.Element("labels");
                    if (labels != null)
                        PatchLocalizedNames(labels, "label", opt.Label);
                    var descs = existingEl.Element("Descriptions");
                    if (descs != null)
                        PatchDescriptions(descs, opt.Description);
                    optionsEl.Add(existingEl);
                }
                else
                {
                    optionsEl.Add(BuildOptionElement(opt));
                }
            }
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
        root.Add(BuildDescriptions(optionSet.Description));

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
        optEl.Add(BuildDescriptions(opt.Description));

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

        // Rebuild — keep order from model
        root.RemoveAll();
        foreach (var rel in relationships)
        {
            var relEl = new XElement("EntityRelationship",
                new XAttribute("Name", rel.SchemaName));
            root.Add(relEl);
        }
    }

    private static XDocument BuildRelationshipsFromScratch(IReadOnlyList<RelationshipMetadata> relationships)
    {
        var root = new XElement("EntityRelationships",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName));

        foreach (var rel in relationships)
        {
            var relEl = new XElement("EntityRelationship",
                new XAttribute("Name", rel.SchemaName));
            root.Add(relEl);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
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
        }
    }

    private static void PatchDescriptions(XElement container, Label label)
    {
        foreach (var kvp in label.LocalizedLabels)
        {
            var existing = container.Elements("Description")
                .FirstOrDefault(e => e.Attribute("languagecode")?.Value == kvp.Key.ToString());
            if (existing != null)
            {
                var descAttr = existing.Attribute("description");
                if (descAttr != null)
                    descAttr.Value = kvp.Value;
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

    private static XElement BuildDescriptions(Label label)
    {
        var container = new XElement("Descriptions");
        foreach (var kvp in label.LocalizedLabels)
        {
            container.Add(new XElement("Description",
                new XAttribute("description", kvp.Value),
                new XAttribute("languagecode", kvp.Key)));
        }
        return container;
    }

    private static void SaveDocument(XDocument doc, string filePath)
    {
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
}
