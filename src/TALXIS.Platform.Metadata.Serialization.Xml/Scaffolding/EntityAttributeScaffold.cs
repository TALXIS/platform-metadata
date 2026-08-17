using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

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

    private static void SetOptionSetOptions(EntityAttributeScaffoldRequest request)
    {
        var options = ParseOptions(request.OptionSetOptions!);
        if (request.GlobalOptionSetFilePath != null)
            SetGlobalOptionSetOptions(request, options);
        else
            SetLocalOptionSetOptions(request.AttributeFilePath, options);
    }

    // Label:Value pairs pin explicit values; bare labels auto-increment from 100000000.
    private static List<OptionMetadata> ParseOptions(string spec)
    {
        var options = new List<OptionMetadata>();
        var nextAutoValue = AutoOptionValueStart;
        foreach (var entry in ParseOptionEntries(spec))
        {
            var match = Regex.Match(entry, @"^(.+):(\d+)$");
            var label = match.Success ? match.Groups[1].Value.Trim() : entry;
            var value = match.Success ? int.Parse(match.Groups[2].Value) : nextAutoValue++;
            options.Add(new OptionMetadata
            {
                Value = value,
                Label = new Label(label),
                Description = new Label(""),
            });
        }
        return options;
    }

    // Global sets go through the workspace model: the reader/writer own the option set
    // file shape and the RootComponent (type 9) registration in Solution.xml.
    private static void SetGlobalOptionSetOptions(EntityAttributeScaffoldRequest request, List<OptionMetadata> options)
    {
        var workspace = new XmlWorkspaceReader().Load(request.SolutionRootPath);

        var name = Path.GetFileNameWithoutExtension(request.GlobalOptionSetFilePath!);
        var optionSet = workspace.GlobalOptionSets.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Global option set '{name}' not found in '{request.SolutionRootPath}'.");
        foreach (var option in options)
        {
            optionSet.AddOption(option);
        }

        if (request.GlobalOptionSetSchemaName != null)
        {
            var solution = workspace.Solutions.FirstOrDefault()
                ?? throw new InvalidOperationException($"No solution manifest found in '{request.SolutionRootPath}'.");
            var exists = solution.RootComponents.Any(rc =>
                rc.Type == ComponentType.OptionSet &&
                string.Equals(rc.SchemaName, request.GlobalOptionSetSchemaName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                solution.AddRootComponent(new RootComponent
                {
                    Type = ComponentType.OptionSet,
                    SchemaName = request.GlobalOptionSetSchemaName,
                    Behavior = 0,
                });
            }
        }

        new XmlWorkspaceWriter().Write(workspace, request.SolutionRootPath);
    }

    // Local sets stay file-level (the rendered attribute is not part of the workspace),
    // but the option XML shape is borrowed from the writer.
    private static void SetLocalOptionSetOptions(string attributeFilePath, List<OptionMetadata> options)
    {
        var doc = XDocument.Load(attributeFilePath);
        var optionsElement = doc.Descendants("options").FirstOrDefault()
            ?? throw new InvalidOperationException($"Options node not found in '{attributeFilePath}'.");
        foreach (var option in options)
        {
            optionsElement.Add(XmlWorkspaceWriter.BuildOptionElement(option));
        }
        SaveXml(doc, attributeFilePath);
    }

    private static IEnumerable<string> ParseOptionEntries(string spec) =>
        spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Replace("{", "").Replace("}", "").Trim())
            .Where(e => e.Length > 0);

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
        using var writer = XmlWriter.Create(path, CreateWriterSettings());
        doc.Save(writer);
    }

    private static void SaveXml(XDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, CreateWriterSettings());
        doc.Save(writer);
    }

    private static XmlWriterSettings CreateWriterSettings() => new()
    {
        Indent = true,
        NewLineHandling = NewLineHandling.None,
        OmitXmlDeclaration = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
    };
}
