using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Writes a CMT package back to data_schema.xml and data.xml, patching the documents it was read from.
/// </summary>
public sealed class CmtPackageXmlWriter
{
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Writes the package schema as data_schema.xml to the supplied writer.
    /// </summary>
    public void WriteSchema(CmtPackage package, XmlWriter writer) => BuildSchema(package).Save(writer);

    /// <summary>
    /// Writes the package data as data.xml to the supplied writer.
    /// </summary>
    public void WriteData(CmtPackage package, XmlWriter writer) => BuildData(package).Save(writer);

    /// <summary>
    /// Saves the package schema to a data_schema.xml file.
    /// </summary>
    public void SaveSchema(CmtPackage package, string path) =>
        SaveIfChanged(package.SchemaDocument, () => BuildSchema(package), path, package.Schema.Source?.FilePath);

    /// <summary>
    /// Saves the package data to a data.xml file.
    /// </summary>
    public void SaveData(CmtPackage package, string path) =>
        SaveIfChanged(package.DataDocument, () => BuildData(package), path, package.Data?.Source?.FilePath);

    // Hand-edited files can contain formatting XDocument cannot reproduce, so an unchanged package is not rewritten.
    private static void SaveIfChanged(XDocument? original, Func<XDocument> build, string path, string? loadedFrom)
    {
        var before = original?.ToString(SaveOptions.DisableFormatting);
        var document = build();
        var isSameFile = loadedFrom is not null && File.Exists(path) && string.Equals(Path.GetFullPath(path), Path.GetFullPath(loadedFrom), StringComparison.OrdinalIgnoreCase);
        if (isSameFile && before == document.ToString(SaveOptions.DisableFormatting)) return;
        Save(document, path, original is null);
    }

    private static XDocument BuildSchema(CmtPackage package)
    {
        var document = package.SchemaDocument ?? NewDocument(new XElement("entities"));
        var root = document.Root!;
        SetString(root, "dateMode", package.Schema.DateMode);
        SyncChildren(root, "entity", package.Schema.Entities, e => e.Name, ApplySchemaEntity);
        SyncImportOrder(root, package.Schema.EntityImportOrder);
        return document;
    }

    private static XDocument BuildData(CmtPackage package)
    {
        var data = package.Data ?? throw new InvalidOperationException("The package has no data.");
        var document = package.DataDocument ?? NewDocument(new XElement("entities",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName)));
        var root = document.Root!;
        SetString(root, "timestamp", data.Timestamp);
        SyncChildren(root, "entity", data.Entities, e => e.Name, ApplyDataEntity);
        return document;
    }

    private static XDocument NewDocument(XElement root) => new(new XDeclaration("1.0", "utf-8", null), root);

    private static void ApplySchemaEntity(CmtSchemaEntity entity, XElement element, bool isNew)
    {
        SetString(element, "name", entity.Name);
        SetString(element, "displayname", entity.DisplayName);
        SetString(element, "etc", entity.ObjectTypeCode?.ToString(CultureInfo.InvariantCulture));
        SetString(element, "primaryidfield", entity.PrimaryIdField);
        SetString(element, "primarynamefield", entity.PrimaryNameField);
        SetBool(element, "disableplugins", entity.DisablePlugins, isNew);
        SetBool(element, "skipupdate", entity.SkipUpdate);
        SetBool(element, "forcecreate", entity.ForceCreate);
        SyncChildren(Container(element, "fields", isNew || entity.Fields.Count > 0), "field", entity.Fields, f => f.Name, ApplySchemaField);
        SyncChildren(Container(element, "relationships", entity.Relationships.Count > 0), "relationship", entity.Relationships, r => r.Name, ApplyRelationship);
        SyncFilter(element, entity.FetchXmlFilter);
    }

    private static void ApplySchemaField(CmtSchemaField field, XElement element, bool isNew)
    {
        SetBool(element, "updateCompare", field.IsUpdateCompare);
        SetString(element, "displayname", field.DisplayName);
        SetString(element, "name", field.Name);
        SetString(element, "type", field.Type);
        SetString(element, "lookupType", field.LookupType);
        SetString(element, "dateMode", field.DateMode);
        SetBool(element, "primaryKey", field.IsPrimaryKey);
        SetBool(element, "customfield", field.IsCustomField);
    }

    private static void ApplyRelationship(CmtSchemaRelationship relationship, XElement element, bool isNew)
    {
        SetString(element, "name", relationship.Name);
        SetBool(element, "manyToMany", relationship.IsManyToMany);
        SetBool(element, "isreflexive", relationship.IsReflexive, isNew && relationship.IsManyToMany);
        SetString(element, "relatedEntityName", relationship.RelatedEntityName);
        SetString(element, "m2mTargetEntity", relationship.M2mTargetEntity);
        SetString(element, "m2mTargetEntityPrimaryKey", relationship.M2mTargetEntityPrimaryKey);
        SetString(element, "referencingAttribute", relationship.ReferencingAttribute);
        SetString(element, "referencedEntity", relationship.ReferencedEntity);
        SetString(element, "referencedAttribute", relationship.ReferencedAttribute);
        SetString(element, "referencingEntity", relationship.ReferencingEntity);
        SyncChildren(Container(element, "fields", relationship.Fields.Count > 0), "field", relationship.Fields, f => f.Name, ApplySchemaField);
    }

    private static void ApplyDataEntity(CmtDataEntity entity, XElement element, bool isNew)
    {
        SetString(element, "name", entity.Name);
        SetString(element, "displayname", entity.DisplayName);
        SyncChildren(Container(element, "records", isNew || entity.Records.Count > 0), "record", entity.Records, r => GuidKey(r.Id), ApplyRecord);
        SyncChildren(Container(element, "m2mrelationships", isNew || entity.ManyToManyRelationships.Count > 0), "m2mrelationship",
            entity.ManyToManyRelationships, m => ManyToManyKey(m.RelationshipName, m.SourceId), ApplyManyToMany);
    }

    private static void ApplyRecord(CmtDataRecord record, XElement element, bool isNew)
    {
        SetGuid(element, "id", record.Id);
        SyncChildren(element, "field", record.Fields, f => f.Name, ApplyDataField);
    }

    private static void ApplyDataField(CmtDataField field, XElement element, bool isNew)
    {
        SetString(element, "name", field.Name);
        SetString(element, "value", field.Value);
        SetString(element, "lookupentity", field.LookupEntity);
        SetString(element, "lookupentityname", field.LookupEntityName);
    }

    private static void ApplyManyToMany(CmtDataManyToManyRelationship m2m, XElement element, bool isNew)
    {
        SetGuid(element, "sourceid", m2m.SourceId);
        SetString(element, "targetentityname", m2m.TargetEntityName);
        SetString(element, "targetentitynameidfield", m2m.TargetEntityNameIdField);
        SetString(element, "m2mrelationshipname", m2m.RelationshipName);

        var targets = Container(element, "targetids", true)!;
        var current = targets.Elements("targetid").Select(e => ParseGuid(e.Value));
        if (!current.SequenceEqual(m2m.TargetIds)) ReplaceValues(targets, "targetid", m2m.TargetIds.Select(id => id.ToString()));
    }

    private static void SyncImportOrder(XElement root, IList<string> order)
    {
        var element = root.Element("entityImportOrder");
        var current = element?.Elements("entityName").Select(e => e.Value) ?? Enumerable.Empty<string>();
        if (current.SequenceEqual(order)) return;

        element ??= Append(root, new XElement("entityImportOrder"));
        ReplaceValues(element, "entityName", order);
    }

    private static void SyncFilter(XElement entity, string? filter)
    {
        var element = entity.Element("filter");
        if (filter is null)
        {
            if (element is not null) Remove(element);
            return;
        }

        if (element is null) Append(entity, new XElement("filter", filter));
        else if (element.Value != filter) element.Value = filter;
    }

    private static void SyncChildren<T>(XElement? container, string name, IList<T> items, Func<T, string> key, Action<T, XElement, bool> apply)
    {
        if (container is null) return;

        var available = new Dictionary<string, Queue<XElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in container.Elements(name))
        {
            var existingKey = ElementKey(existing);
            if (!available.TryGetValue(existingKey, out var queue)) available[existingKey] = queue = new Queue<XElement>();
            queue.Enqueue(existing);
        }

        XElement? previous = null;
        foreach (var item in items)
        {
            var isNew = !available.TryGetValue(key(item), out var matches) || matches.Count == 0;
            var element = isNew ? Insert(container, name, previous, new XElement(name)) : matches!.Dequeue();
            apply(item, element, isNew);
            previous = element;
        }

        foreach (var stale in available.Values.SelectMany(q => q).ToList()) Remove(stale);
    }

    private static string ElementKey(XElement element) => element.Name.LocalName switch
    {
        "record" => GuidKey(ParseGuid(element.Attribute("id")?.Value)),
        "m2mrelationship" => ManyToManyKey(element.Attribute("m2mrelationshipname")?.Value, ParseGuid(element.Attribute("sourceid")?.Value)),
        _ => element.Attribute("name")?.Value ?? string.Empty
    };

    private static string GuidKey(Guid id) => id.ToString();

    private static string ManyToManyKey(string? relationshipName, Guid sourceId) => relationshipName + "|" + sourceId;

    private static XElement? Container(XElement parent, string name, bool create)
    {
        var container = parent.Element(name);
        if (container is null && create) container = Append(parent, new XElement(name));
        return container;
    }

    private static void ReplaceValues(XElement container, string name, IEnumerable<string> values)
    {
        var list = values.ToList();
        foreach (var element in container.Elements(name).ToList()) Remove(element);
        foreach (var value in list) Append(container, new XElement(name, value));
    }

    private static XElement Insert(XElement container, string name, XElement? previous, XElement element)
    {
        if (previous is not null)
        {
            previous.AddAfterSelf(element);
            if (LeadingWhitespace(previous) is { } indent) previous.AddAfterSelf(new XText(indent));
            return element;
        }

        var first = container.Elements(name).FirstOrDefault();
        if (first is null) return Append(container, element);

        first.AddBeforeSelf(element);
        if (LeadingWhitespace(element) is { } firstIndent) first.AddBeforeSelf(new XText(firstIndent));
        return element;
    }

    private static XElement Append(XElement container, XElement element)
    {
        var last = container.Elements().LastOrDefault();
        if (last is not null)
        {
            last.AddAfterSelf(element);
            if (LeadingWhitespace(last) is { } indent) last.AddAfterSelf(new XText(indent));
            return element;
        }

        var outer = LeadingWhitespace(container);
        if (outer is null || container.Nodes().Any(n => !IsWhitespace(n)))
        {
            container.Add(element);
            return element;
        }

        var unit = outer.IndexOf('\t') >= 0 ? "\t" : "  ";
        container.RemoveNodes();
        container.Add(new XText(outer + unit), element, new XText(outer));
        return element;
    }

    private static void Remove(XElement element)
    {
        if (element.PreviousNode is { } previous && IsWhitespace(previous)) previous.Remove();
        element.Remove();
    }

    private static string? LeadingWhitespace(XElement element) =>
        element.PreviousNode is XText text && IsWhitespace(text) && text.Value.IndexOf('\n') >= 0 ? text.Value : null;

    private static bool IsWhitespace(XNode node) => node is XText text && node is not XCData && string.IsNullOrWhiteSpace(text.Value);

    private static void SetString(XElement element, string name, string? value)
    {
        if (element.Attribute(name)?.Value == value) return;
        element.SetAttributeValue(name, value);
    }

    private static void SetBool(XElement element, string name, bool value, bool writeFalse = false)
    {
        var current = element.Attribute(name);
        if (current is null ? !value && !writeFalse : ParseBool(current.Value) == value) return;
        element.SetAttributeValue(name, value ? "true" : "false");
    }

    private static void SetGuid(XElement element, string name, Guid value)
    {
        if (Guid.TryParse(element.Attribute(name)?.Value, out var current) && current == value) return;
        element.SetAttributeValue(name, value.ToString());
    }

    private static bool ParseBool(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static Guid ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : Guid.Empty;

    private static void Save(XDocument document, string path, bool isNew)
    {
        var existing = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var newLine = existing is null ? "\r\n" : DetectNewLine(existing);
        var encodingName = document.Declaration?.Encoding;
        var settings = new XmlWriterSettings
        {
            Encoding = encodingName is null || encodingName.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
                ? new UTF8Encoding(existing is not null && HasBom(existing))
                : Encoding.GetEncoding(encodingName),
            OmitXmlDeclaration = document.Declaration is null,
            NewLineChars = newLine,
            NewLineHandling = NewLineHandling.Replace,
            Indent = isNew,
            IndentChars = "  "
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings)) document.Save(writer);

        var text = settings.Encoding.GetString(buffer.ToArray());
        if (KeepsSpacedEmptyRoot(document, existing, settings.Encoding)) text = ReplaceFirst(text, "<entities>", "<entities >");
        File.WriteAllBytes(path, settings.Encoding.GetBytes(text));
    }

    // The CMT tool writes an attribute-less schema root as "<entities >", which XDocument cannot preserve.
    private static bool KeepsSpacedEmptyRoot(XDocument document, byte[]? existing, Encoding encoding) =>
        document.Root is { Name.LocalName: "entities" } root
        && !root.HasAttributes
        && (existing is null || encoding.GetString(existing).Contains("<entities >"));

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0 ? text : text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
    }

    private static bool HasBom(byte[] bytes) => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static string DetectNewLine(byte[] bytes)
    {
        var index = Array.IndexOf(bytes, (byte)'\n');
        if (index < 0) return "\r\n";
        return index > 0 && bytes[index - 1] == (byte)'\r' ? "\r\n" : "\n";
    }
}
