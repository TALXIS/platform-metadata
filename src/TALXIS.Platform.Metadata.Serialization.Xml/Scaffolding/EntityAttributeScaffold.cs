using System.Xml;
using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-entity-attribute template post-action scripts.
/// Mirrors their behavior step by step: set option set options, import the attribute
/// into Entity.xml, add money support attributes, add the lookup relationship files,
/// sort entity attributes, and normalize xsi:nil tags in Solution.xml.
/// Transitional adapter: patches files directly for byte-compatible output with the old
/// scripts; steps migrate onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class EntityAttributeScaffold
{
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

        var entityXmlPath = Path.Combine(request.SolutionRootPath, SolutionPackagerLayout.EntitiesDirectory, request.EntitySchemaName, "Entity.xml");
        if (!File.Exists(entityXmlPath))
            throw new FileNotFoundException($"Entity.xml not found: {entityXmlPath}");

        ImportAttribute(entityXmlPath, request.AttributeFilePath, result);

        if (request.MoneyBaseAttributeFilePath != null)
            AddMoneySupport(entityXmlPath, request);

        if (request.LookupRelationshipFilePath != null)
            AddLookupRelationship(request, result);

        // Cross-cutting normalization passes over the whole solution.
        EntityAttributeSorter.SortAll(request.SolutionRootPath);
        NilTagNormalizer.NormalizeSolutionXml(request.SolutionRootPath);

        return result;
    }

    private static void SetOptionSetOptions(EntityAttributeScaffoldRequest request)
    {
        var options = OptionSetOptionsApplier.ParseOptions(request.OptionSetOptions!);
        if (request.GlobalOptionSetFilePath != null)
        {
            OptionSetOptionsApplier.ApplyToGlobalOptionSet(
                request.SolutionRootPath,
                Path.GetFileNameWithoutExtension(request.GlobalOptionSetFilePath),
                request.GlobalOptionSetSchemaName,
                options);
        }
        else
        {
            OptionSetOptionsApplier.ApplyToLocalAttribute(request.AttributeFilePath, options);
        }
    }

    // Appends the rendered <attribute> into Entity.xml; skips with a warning when an
    // attribute with the same LogicalName already exists (never overwrites metadata).
    private static void ImportAttribute(string entityXmlPath, string attributeFilePath, ScaffoldResult result)
    {
        var entityDoc = ScaffoldXmlFile.Load(entityXmlPath);
        var attributeDoc = ScaffoldXmlFile.Load(attributeFilePath);

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
        ScaffoldXmlFile.Save(entityDoc, entityXmlPath);
    }

    // Money columns need up to three support attributes: transactioncurrencyid and
    // exchangerate (only on full entities) plus the _base shadow column (always).
    private static void AddMoneySupport(string entityXmlPath, EntityAttributeScaffoldRequest request)
    {
        var entityDoc = ScaffoldXmlFile.Load(entityXmlPath);
        var container = GetAttributesContainer(entityDoc, entityXmlPath);

        // Stub entities (attributes only) must not receive the shared currency/exchange columns.
        var hasFullEntityMetadata = entityDoc.SelectSingleNode("/Entity/EntityInfo/entity/LocalizedNames") != null;
        if (hasFullEntityMetadata)
        {
            AppendAttributeIfMissing(entityDoc, container, request.CurrencyAttributeFilePath);
            AppendAttributeIfMissing(entityDoc, container, request.ExchangeRateAttributeFilePath);
        }

        AppendAttributeIfMissing(entityDoc, container, request.MoneyBaseAttributeFilePath);
        ScaffoldXmlFile.Save(entityDoc, entityXmlPath);
    }

    private static void AppendAttributeIfMissing(XmlDocument entityDoc, XmlNode container, string? attributeFilePath)
    {
        if (attributeFilePath == null) return;

        var attributeDoc = ScaffoldXmlFile.Load(attributeFilePath);
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

        var otherDir = Path.Combine(request.SolutionRootPath, SolutionPackagerLayout.OtherDirectory);
        var referencedEntityFilePath = Path.Combine(otherDir, "Relationships", $"{referencedEntity}.xml");
        var relationshipsFilePath = Path.Combine(request.SolutionRootPath, SolutionPackagerLayout.RelationshipsXmlPath);

        RelationshipsXmlFile.EnsureExists(referencedEntityFilePath);
        RelationshipsXmlFile.EnsureExists(relationshipsFilePath);

        var referencedDoc = ScaffoldXmlFile.Load(referencedEntityFilePath);
        if (RelationshipsXmlFile.ContainsRelationship(referencedDoc, relationshipName))
        {
            result.AddWarning($"Relationship '{relationshipName}' already exists in '{referencedEntityFilePath}' - skipping.");
        }
        else
        {
            var templateDoc = ScaffoldXmlFile.Load(request.LookupRelationshipFilePath!);
            var relationshipElement = templateDoc.DocumentElement
                ?? throw new InvalidOperationException($"No root element in '{request.LookupRelationshipFilePath}'.");
            referencedDoc.DocumentElement!.AppendChild(referencedDoc.ImportNode(relationshipElement, deep: true));
        }

        var relationshipsDoc = ScaffoldXmlFile.Load(relationshipsFilePath);
        if (RelationshipsXmlFile.ContainsRelationship(relationshipsDoc, relationshipName))
        {
            result.AddWarning($"Relationship '{relationshipName}' already exists in '{relationshipsFilePath}' - skipping.");
        }
        else
        {
            RelationshipsXmlFile.AppendNameStub(relationshipsDoc, relationshipName);
        }

        ScaffoldXmlFile.Save(relationshipsDoc, relationshipsFilePath);
        ScaffoldXmlFile.Save(referencedDoc, referencedEntityFilePath);
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
}
