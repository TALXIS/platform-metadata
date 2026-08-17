using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-entity-attribute template post-action scripts.
/// Mirrors their behavior step by step: set option set options, import the attribute
/// into Entity.xml, add money support attributes, add the lookup relationship files,
/// sort entity attributes, and normalize xsi:nil tags in Solution.xml.
/// </summary>
public static class EntityAttributeScaffold
{
    private const int AutoOptionValueStart = 100000000;
    private const string EmptyRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><EntityRelationships xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"></EntityRelationships>";

    public static ScaffoldResult Apply(EntityAttributeScaffoldRequest request)
    {
        if (!Directory.Exists(request.SolutionRootPath))
            throw new DirectoryNotFoundException($"Solution root not found: {request.SolutionRootPath}");
        if (!File.Exists(request.AttributeFilePath))
            throw new FileNotFoundException($"Attribute file not found: {request.AttributeFilePath}");

        var result = new ScaffoldResult();

        // Step order mirrors the original template post-actions.
        if (!string.IsNullOrEmpty(request.OptionSetOptions))
            SetOptionSetOptions(request);

        var entityXmlPath = Path.Combine(request.SolutionRootPath, "Entities", request.EntitySchemaName, "Entity.xml");
        if (!File.Exists(entityXmlPath))
            throw new FileNotFoundException($"Entity.xml not found: {entityXmlPath}");

        ImportAttribute(entityXmlPath, request.AttributeFilePath, result);

        if (request.MoneyBaseAttributeFilePath != null)
            AddMoneySupport(entityXmlPath, request);

        if (request.LookupRelationshipFilePath != null)
            AddLookupRelationship(request, result);

        // Cross-cutting normalization passes over the whole solution.
        SortEntityAttributes(request.SolutionRootPath);
        NormalizeNilTags(request.SolutionRootPath);

        return result;
    }

    // Fills the empty <options> element of the rendered option set with parsed options.
    // Local sets live inside the attribute file, global sets in OptionSets/<name>.xml.
    private static void SetOptionSetOptions(EntityAttributeScaffoldRequest request)
    {
        var targetPath = request.GlobalOptionSetFilePath ?? request.AttributeFilePath;
        var doc = LoadXml(targetPath);

        var optionsNode = doc.SelectSingleNode("//options")
            ?? throw new InvalidOperationException($"Options node not found in '{targetPath}'.");

        var nextAutoValue = (long)AutoOptionValueStart;
        foreach (var entry in ParseOptionEntries(request.OptionSetOptions!))
        {
            // Label:Value pairs pin explicit values; bare labels auto-increment from 100000000.
            long value;
            string label;
            var match = Regex.Match(entry, @"^(.+):(\d+)$");
            if (match.Success)
            {
                label = match.Groups[1].Value.Trim();
                value = long.Parse(match.Groups[2].Value);
            }
            else
            {
                label = entry;
                value = nextAutoValue++;
            }

            optionsNode.AppendChild(BuildOptionElement(doc, value, label));
        }

        SaveXml(doc, targetPath);

        if (request.GlobalOptionSetFilePath != null && request.GlobalOptionSetSchemaName != null)
            EnsureGlobalOptionSetRootComponent(request);
    }

    private static IEnumerable<string> ParseOptionEntries(string spec) =>
        spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Replace("{", "").Replace("}", "").Trim())
            .Where(e => e.Length > 0);

    private static XmlElement BuildOptionElement(XmlDocument doc, long value, string label)
    {
        var option = doc.CreateElement("option");
        option.SetAttribute("value", value.ToString());
        option.SetAttribute("ExternalValue", "");
        option.SetAttribute("IsHidden", "0");

        var labels = doc.CreateElement("labels");
        var labelElement = doc.CreateElement("label");
        labelElement.SetAttribute("description", label);
        labelElement.SetAttribute("languagecode", "1033");
        labels.AppendChild(labelElement);
        option.AppendChild(labels);

        var descriptions = doc.CreateElement("Descriptions");
        var description = doc.CreateElement("Description");
        description.SetAttribute("description", "");
        description.SetAttribute("languagecode", "1033");
        descriptions.AppendChild(description);
        option.AppendChild(descriptions);

        return option;
    }

    // A new global option set must also be registered in Solution.xml as RootComponent type 9.
    private static void EnsureGlobalOptionSetRootComponent(EntityAttributeScaffoldRequest request)
    {
        var solutionPath = Path.Combine(request.SolutionRootPath, "Other", "Solution.xml");
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution.xml not found: {solutionPath}");

        var doc = LoadXml(solutionPath);
        var schemaName = request.GlobalOptionSetSchemaName!;
        if (doc.SelectSingleNode($"//RootComponent[@type='9' and @schemaName='{schemaName}']") != null) return;

        var rootComponents = doc.SelectSingleNode("//RootComponents")
            ?? throw new InvalidOperationException($"RootComponents node not found in '{solutionPath}'.");

        var component = doc.CreateElement("RootComponent");
        component.SetAttribute("type", "9");
        component.SetAttribute("schemaName", schemaName);
        component.SetAttribute("behavior", "0");
        rootComponents.AppendChild(component);

        doc.Save(solutionPath);
    }

    // Appends the rendered <attribute> into Entity.xml; skips with a warning when an
    // attribute with the same LogicalName already exists (never overwrites metadata).
    private static void ImportAttribute(string entityXmlPath, string attributeFilePath, ScaffoldResult result)
    {
        var entityDoc = LoadXml(entityXmlPath);
        var attributeDoc = LoadXml(attributeFilePath);

        var attributeElement = attributeDoc.DocumentElement
            ?? throw new InvalidOperationException($"No root element in '{attributeFilePath}'.");
        var logicalName = attributeElement.SelectSingleNode("LogicalName")?.InnerText;

        var container = GetAttributesContainer(entityDoc, entityXmlPath);
        if (AttributeExists(container, logicalName))
        {
            result.AddWarning($"Attribute '{logicalName}' already exists in '{entityXmlPath}'. Skipping attribute append; existing metadata was not overwritten.");
            return;
        }

        container.AppendChild(entityDoc.ImportNode(attributeElement, deep: true));
        SaveXml(entityDoc, entityXmlPath);
    }

    // Money columns need up to three support attributes: transactioncurrencyid and
    // exchangerate (only on full entities) plus the _base shadow column (always).
    private static void AddMoneySupport(string entityXmlPath, EntityAttributeScaffoldRequest request)
    {
        var entityDoc = LoadXml(entityXmlPath);
        var container = GetAttributesContainer(entityDoc, entityXmlPath);

        // Stub entities (attributes only) must not receive the shared currency/exchange columns.
        var hasFullEntityMetadata = entityDoc.SelectSingleNode("/Entity/EntityInfo/entity/LocalizedNames") != null;
        if (hasFullEntityMetadata)
        {
            AppendAttributeIfMissing(entityDoc, container, request.CurrencyAttributeFilePath);
            AppendAttributeIfMissing(entityDoc, container, request.ExchangeRateAttributeFilePath);
        }

        AppendAttributeIfMissing(entityDoc, container, request.MoneyBaseAttributeFilePath);
        SaveXml(entityDoc, entityXmlPath);
    }

    private static void AppendAttributeIfMissing(XmlDocument entityDoc, XmlNode container, string? attributeFilePath)
    {
        if (attributeFilePath == null) return;

        var attributeDoc = LoadXml(attributeFilePath);
        var attributeElement = attributeDoc.DocumentElement
            ?? throw new InvalidOperationException($"No root element in '{attributeFilePath}'.");
        var logicalName = attributeElement.SelectSingleNode("LogicalName")?.InnerText;

        if (!AttributeExists(container, logicalName))
            container.AppendChild(entityDoc.ImportNode(attributeElement, deep: true));
    }

    // A lookup produces two files: the full relationship in Other/Relationships/<target>.xml
    // and a name-only stub in the Other/Relationships.xml index. Both steps are idempotent.
    private static void AddLookupRelationship(EntityAttributeScaffoldRequest request, ScaffoldResult result)
    {
        var relationshipName = request.LookupRelationshipName
            ?? throw new InvalidOperationException("LookupRelationshipName is required when LookupRelationshipFilePath is set.");
        var referencedEntity = request.ReferencedEntityName
            ?? throw new InvalidOperationException("ReferencedEntityName is required when LookupRelationshipFilePath is set.");

        var otherDir = Path.Combine(request.SolutionRootPath, "Other");
        var referencedEntityFilePath = Path.Combine(otherDir, "Relationships", $"{referencedEntity}.xml");
        var relationshipsFilePath = Path.Combine(otherDir, "Relationships.xml");

        EnsureRelationshipsFile(referencedEntityFilePath);
        EnsureRelationshipsFile(relationshipsFilePath);

        var referencedDoc = LoadXml(referencedEntityFilePath);
        if (RelationshipExists(referencedDoc, relationshipName))
        {
            result.AddWarning($"Relationship '{relationshipName}' already exists in '{referencedEntityFilePath}' - skipping.");
        }
        else
        {
            var templateDoc = LoadXml(request.LookupRelationshipFilePath!);
            var relationshipElement = templateDoc.DocumentElement
                ?? throw new InvalidOperationException($"No root element in '{request.LookupRelationshipFilePath}'.");
            referencedDoc.DocumentElement!.AppendChild(referencedDoc.ImportNode(relationshipElement, deep: true));
        }

        var relationshipsDoc = LoadXml(relationshipsFilePath);
        if (RelationshipExists(relationshipsDoc, relationshipName))
        {
            result.AddWarning($"Relationship '{relationshipName}' already exists in '{relationshipsFilePath}' - skipping.");
        }
        else
        {
            var stub = relationshipsDoc.CreateElement("EntityRelationship");
            stub.SetAttribute("Name", relationshipName);
            relationshipsDoc.DocumentElement!.AppendChild(stub);
        }

        SaveXml(relationshipsDoc, relationshipsFilePath);
        SaveXml(referencedDoc, referencedEntityFilePath);
    }

    private static void EnsureRelationshipsFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        if (File.Exists(path)) return;

        var doc = new XmlDocument();
        doc.LoadXml(EmptyRelationshipsXml);
        doc.Save(path);
    }

    private static bool RelationshipExists(XmlDocument doc, string relationshipName)
    {
        foreach (XmlElement node in doc.GetElementsByTagName("EntityRelationship"))
        {
            if (node.GetAttribute("Name") == relationshipName) return true;
        }
        return false;
    }

    // SolutionPackager keeps attributes sorted by PhysicalName; re-sort every Entity.xml.
    private static void SortEntityAttributes(string solutionRootPath)
    {
        var entitiesDir = Path.Combine(solutionRootPath, "Entities");
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityXmlPath in Directory.GetFiles(entitiesDir, "Entity.xml", SearchOption.AllDirectories))
        {
            var doc = LoadXml(entityXmlPath);
            foreach (XmlNode attributesNode in doc.SelectNodes("//entity/attributes")!)
            {
                var attributes = attributesNode.SelectNodes("attribute")!.Cast<XmlElement>().ToList();
                if (attributes.Count == 0) continue;

                var sorted = attributes.OrderBy(a => a.GetAttribute("PhysicalName").ToLowerInvariant()).ToList();
                foreach (var attribute in attributes)
                {
                    attributesNode.RemoveChild(attribute);
                }
                foreach (var attribute in sorted)
                {
                    attributesNode.AppendChild(attribute);
                }
            }
            SaveXml(doc, entityXmlPath);
        }
    }

    // The template engine splits <Tag xsi:nil="true"></Tag> across two lines; collapse it back.
    private static void NormalizeNilTags(string solutionRootPath)
    {
        var solutionPath = Path.Combine(solutionRootPath, "Other", "Solution.xml");
        if (!File.Exists(solutionPath)) return;

        var content = File.ReadAllText(solutionPath);
        content = Regex.Replace(content, "(xsi:nil=\"true\")>\\s*\\r?\\n\\s*</", "$1></");
        File.WriteAllText(solutionPath, content);
    }

    private static XmlNode GetAttributesContainer(XmlDocument entityDoc, string entityXmlPath) =>
        entityDoc.SelectSingleNode("/Entity/EntityInfo/entity/attributes")
            ?? throw new InvalidOperationException($"Attributes container not found in '{entityXmlPath}'.");

    private static bool AttributeExists(XmlNode container, string? logicalName)
    {
        foreach (XmlNode child in container.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element || child.Name != "attribute") continue;
            if (child.SelectSingleNode("LogicalName")?.InnerText == logicalName) return true;
        }
        return false;
    }

    private static XmlDocument LoadXml(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    // Same writer settings as the original scripts, so output formatting stays byte-compatible.
    private static void SaveXml(XmlDocument doc, string path)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        };
        using var writer = XmlWriter.Create(path, settings);
        doc.Save(writer);
    }
}
