using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Writes a CMT package back to data_schema.xml and data.xml, patching the documents it was read from so
/// unknown attributes and elements, comments, attribute order and indentation survive and Load → Save is a
/// zero-byte diff. Elements are emitted in CMT's order (entityImportOrder after the entities; fields,
/// relationships, filter inside an entity). Packages created in memory get new, indented documents.
/// </summary>
public sealed class CmtPackageXmlWriter
{
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Saves the schema and, when the package has data, the data file into <paramref name="packageDirectory"/>
    /// under CMT's fixed names (<see cref="CmtPackageLayout"/>). Returns whether any file was written.
    /// </summary>
    /// <param name="package">The package to save.</param>
    /// <param name="packageDirectory">Target folder; created when missing.</param>
    public bool Save(CmtPackage package, string packageDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        var written = SaveSchema(package, Path.Combine(packageDirectory, CmtPackageLayout.SchemaFileName));
        if (package.Data != null) written |= SaveData(package, Path.Combine(packageDirectory, CmtPackageLayout.DataFileName));
        return written;
    }

    /// <summary>
    /// Saves the package schema to a data_schema.xml file, keeping the existing file's BOM, newline style and
    /// declaration. Returns <c>false</c> when the file already holds the same content.
    /// </summary>
    /// <param name="package">The package to save.</param>
    /// <param name="path">Target file path.</param>
    public bool SaveSchema(CmtPackage package, string path)
    {
        return WriteIfChanged(BuildSchema(package), package.SchemaBytes, path, indent: package.SchemaDocument == null);
    }

    /// <summary>Saves the package data to a data.xml file; same contract as <see cref="SaveSchema"/>. Throws when the package has no data.</summary>
    /// <param name="package">The package to save.</param>
    /// <param name="path">Target file path.</param>
    public bool SaveData(CmtPackage package, string path)
    {
        return WriteIfChanged(BuildData(package), package.DataBytes, path, indent: package.DataDocument == null);
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

    private static void ApplySchemaEntity(CmtSchemaEntity entity, XElement element)
    {
        // A new entity gets the empty <fields> CMT always writes.
        var isNew = element.IsEmpty && !element.HasAttributes;
        SetString(element, "name", entity.Name);
        SetString(element, "displayname", entity.DisplayName);
        SetString(element, "etc", entity.ObjectTypeCode?.ToString(CultureInfo.InvariantCulture));
        SetString(element, "primaryidfield", entity.PrimaryIdField);
        SetString(element, "primarynamefield", entity.PrimaryNameField);
        SetNullableBool(element, "disableplugins", entity.DisablePlugins);
        SetNullableBool(element, "skipupdate", entity.SkipUpdate);
        SetNullableBool(element, "forcecreate", entity.ForceCreate);
        SetNullableBool(element, "renderliquid", entity.RenderLiquid);
        CmtOtherAttributes.Write(element, entity.OtherAttributes, CmtOtherAttributes.SchemaEntity);
        SyncChildren(Container(element, "fields", isNew || entity.Fields.Count > 0), "field", entity.Fields, f => f.Name, ApplySchemaField);
        SyncChildren(Container(element, "relationships", entity.Relationships.Count > 0), "relationship", entity.Relationships, r => r.Name, ApplyRelationship);
        SyncFilter(element, entity.FetchXmlFilter);
    }

    private static void ApplySchemaField(CmtSchemaField field, XElement element)
    {
        SetBool(element, "updateCompare", field.IsUpdateCompare);
        SetString(element, "displayname", field.DisplayName);
        SetString(element, "name", field.Name);
        if (field.Type.Length > 0 || element.Attribute("type") != null) SetString(element, "type", field.Type);
        SetString(element, "lookupType", field.LookupType);
        SetString(element, "dateMode", field.DateMode);
        SetBool(element, "primaryKey", field.IsPrimaryKey);
        SetBool(element, "customfield", field.IsCustomField);
        CmtOtherAttributes.Write(element, field.OtherAttributes, CmtOtherAttributes.SchemaField);
    }

    private static void ApplyRelationship(CmtSchemaRelationship relationship, XElement element)
    {
        SetString(element, "name", relationship.Name);
        SetBool(element, "manyToMany", relationship.IsManyToMany);
        SetNullableBool(element, "isreflexive", relationship.IsReflexive);
        SetString(element, "relatedEntityName", relationship.RelatedEntityName);
        SetString(element, "m2mTargetEntity", relationship.M2mTargetEntity);
        SetString(element, "m2mTargetEntityPrimaryKey", relationship.M2mTargetEntityPrimaryKey);
        SetString(element, "referencingAttribute", relationship.ReferencingAttribute);
        SetString(element, "referencedEntity", relationship.ReferencedEntity);
        SetString(element, "referencedAttribute", relationship.ReferencedAttribute);
        SetString(element, "referencingEntity", relationship.ReferencingEntity);
        CmtOtherAttributes.Write(element, relationship.OtherAttributes, CmtOtherAttributes.Relationship);
        SyncChildren(Container(element, "fields", relationship.Fields.Count > 0), "field", relationship.Fields, f => f.Name, ApplySchemaField);
    }

    private static void ApplyDataEntity(CmtDataEntity entity, XElement element)
    {
        // A new entity gets the empty <records> and <m2mrelationships> CMT always writes.
        var isNew = element.IsEmpty && !element.HasAttributes;
        SetString(element, "name", entity.Name);
        SetString(element, "displayname", entity.DisplayName);
        CmtOtherAttributes.Write(element, entity.OtherAttributes, CmtOtherAttributes.DataEntity);
        SyncChildren(Container(element, "records", isNew || entity.Records.Count > 0), "record", entity.Records, r => GuidKey(r.Id), ApplyRecord);
        SyncChildren(Container(element, "m2mrelationships", isNew || entity.ManyToManyRelationships.Count > 0), "m2mrelationship",
            entity.ManyToManyRelationships, m => ManyToManyKey(m.RelationshipName, m.SourceId), ApplyManyToMany);
    }

    private static void ApplyRecord(CmtDataRecord record, XElement element)
    {
        // Activity parties may have no id (read as Guid.Empty); records always carry one.
        if (record.Id != Guid.Empty || element.Name.LocalName == "record") SetGuid(element, "id", record.Id);
        SetNullableGuid(element, "newId", record.NewId);
        CmtOtherAttributes.Write(element, record.OtherAttributes, CmtOtherAttributes.Record);
        SyncChildren(element, "field", record.Fields, f => f.Name, ApplyDataField);
    }

    private static void ApplyDataField(CmtDataField field, XElement element)
    {
        SetString(element, "name", field.Name);
        SetString(element, "value", field.Value);
        SetString(element, "filename", field.FileName);
        SetString(element, "lookupentity", field.LookupEntity);
        SetString(element, "lookupentityname", field.LookupEntityName);
        CmtOtherAttributes.Write(element, field.OtherAttributes, CmtOtherAttributes.DataField);
        // Partylist: one <activitypointerrecords> element per activity party, directly under the field.
        SyncChildren(element, "activitypointerrecords", field.ActivityPointerRecords, r => GuidKey(r.Id), ApplyRecord);
    }

    private static void ApplyManyToMany(CmtDataManyToManyRelationship m2m, XElement element)
    {
        SetGuid(element, "sourceid", m2m.SourceId);
        SetString(element, "targetentityname", m2m.TargetEntityName);
        SetString(element, "targetentitynameidfield", m2m.TargetEntityNameIdField);
        SetString(element, "m2mrelationshipname", m2m.RelationshipName);
        SetString(element, "m2mrelationshipschemaname", m2m.RelationshipSchemaName);
        CmtOtherAttributes.Write(element, m2m.OtherAttributes, CmtOtherAttributes.ManyToMany);

        var targets = Container(element, "targetids", create: true)!;
        var current = targets.Elements("targetid").Select(e => CmtPackageXmlReader.ParseGuid(e.Value) ?? Guid.Empty);
        if (!current.SequenceEqual(m2m.TargetIds)) XmlPatch.ReplaceValues(targets, "targetid", m2m.TargetIds.Select(id => id.ToString()));
    }

    private static void SyncImportOrder(XElement root, IList<string> order)
    {
        var element = root.Element("entityImportOrder");
        var current = element?.Elements("entityName").Select(e => e.Value) ?? Enumerable.Empty<string>();
        if (current.SequenceEqual(order)) return;

        element ??= XmlPatch.Append(root, new XElement("entityImportOrder"));
        XmlPatch.ReplaceValues(element, "entityName", order);
    }

    private static void SyncFilter(XElement entity, string? filter)
    {
        var element = entity.Element("filter");
        if (filter == null)
        {
            if (element != null) XmlPatch.Remove(element);
            return;
        }

        if (element == null) XmlPatch.Append(entity, new XElement("filter", filter));
        else if (element.Value != filter) element.Value = filter;
    }

    private static void SyncChildren<T>(XElement? container, string name, IList<T> items, Func<T, string> key, Action<T, XElement> apply)
    {
        if (container == null) return;

        var available = new Dictionary<string, Queue<XElement>>(StringComparer.Ordinal);
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
            var element = isNew ? XmlPatch.Insert(container, name, previous, new XElement(name)) : matches!.Dequeue();
            if (!isNew) XmlPatch.MoveAfter(container, name, previous, element);
            apply(item, element);
            previous = element;
        }

        foreach (var stale in available.Values.SelectMany(q => q).ToList()) XmlPatch.Remove(stale);
    }

    private static string ElementKey(XElement element) => element.Name.LocalName switch
    {
        "m2mrelationship" => ManyToManyKey(element.Attribute("m2mrelationshipname")?.Value, CmtPackageXmlReader.ParseGuid(element.Attribute("sourceid")?.Value) ?? Guid.Empty),
        "record" or "activitypointerrecords" => GuidKey(CmtPackageXmlReader.ParseGuid(element.Attribute("id")?.Value) ?? Guid.Empty),
        _ => element.Attribute("name")?.Value ?? string.Empty
    };

    private static string GuidKey(Guid id) => id.ToString();

    private static string ManyToManyKey(string? relationshipName, Guid sourceId) => relationshipName + "|" + sourceId;

    private static XElement? Container(XElement parent, string name, bool create)
    {
        var container = parent.Element(name);
        if (container == null && create) container = XmlPatch.Append(parent, new XElement(name));
        return container;
    }

    private static void SetString(XElement element, string name, string? value)
    {
        if (element.Attribute(name)?.Value == value) return;
        element.SetAttributeValue(name, value);
    }

    // "Absent means false" flags: false is never written, an existing attribute is only touched when its meaning changes.
    private static void SetBool(XElement element, string name, bool value)
    {
        var current = element.Attribute(name);
        if (current == null ? !value : CmtPackageXmlReader.ParseBool(current.Value) == value) return;
        element.SetAttributeValue(name, value ? "true" : "false");
    }

    // Absent attribute <=> null; otherwise written as true/false, keeping the existing lexical form when it already means the same.
    private static void SetNullableBool(XElement element, string name, bool? value)
    {
        var current = element.Attribute(name);
        if (value == null)
        {
            current?.Remove();
            return;
        }

        if (current != null && CmtPackageXmlReader.ParseBool(current.Value) == value.Value) return;
        element.SetAttributeValue(name, value.Value ? "true" : "false");
    }

    private static void SetGuid(XElement element, string name, Guid value)
    {
        if (Guid.TryParse(element.Attribute(name)?.Value, out var current) && current == value) return;
        element.SetAttributeValue(name, value.ToString());
    }

    private static void SetNullableGuid(XElement element, string name, Guid? value)
    {
        if (value == null) element.Attribute(name)?.Remove();
        else SetGuid(element, name, value.Value);
    }

    // Hand-edited files can contain formatting XDocument cannot reproduce (attributes split over lines, for
    // example), so a document that still matches the file it was loaded from is saved as that file's bytes.
    private static bool WriteIfChanged(XDocument document, byte[]? loadedBytes, string path, bool indent)
    {
        var existing = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var bytes = loadedBytes != null && IsUnchanged(document, loadedBytes)
            ? loadedBytes
            : Serialize(document, existing, indent);
        if (existing != null && existing.SequenceEqual(bytes)) return false;

        File.WriteAllBytes(path, bytes);
        return true;
    }

    private static bool IsUnchanged(XDocument document, byte[] loadedBytes)
    {
        var loaded = CmtPackageXmlReader.Parse(loadedBytes);
        return loaded.ToString(SaveOptions.DisableFormatting) == document.ToString(SaveOptions.DisableFormatting);
    }

    private static byte[] Serialize(XDocument document, byte[]? existing, bool indent)
    {
        var newLine = existing == null ? "\r\n" : DetectNewLine(existing);
        var encodingName = document.Declaration?.Encoding;
        var settings = new XmlWriterSettings
        {
            Encoding = encodingName == null || encodingName.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
                ? new UTF8Encoding(existing != null && HasBom(existing))
                : Encoding.GetEncoding(encodingName),
            OmitXmlDeclaration = document.Declaration == null,
            NewLineChars = newLine,
            NewLineHandling = NewLineHandling.Replace,
            Indent = indent,
            IndentChars = "  "
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings)) document.Save(writer);

        var text = settings.Encoding.GetString(buffer.ToArray());
        if (KeepsSpacedEmptyRoot(document, existing, settings.Encoding)) text = ReplaceFirst(text, "<entities>", "<entities >");
        // Keep a declaration without encoding (<?xml version="1.0"?>) as loaded; XmlWriter always adds one.
        if (document.Declaration != null && string.IsNullOrEmpty(document.Declaration.Encoding))
            text = ReplaceFirst(text, "<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<?xml version=\"1.0\"?>");
        return settings.Encoding.GetBytes(text);
    }

    // The CMT tool writes an attribute-less schema root as "<entities >", which XDocument cannot preserve.
    private static bool KeepsSpacedEmptyRoot(XDocument document, byte[]? existing, Encoding encoding)
    {
        var root = document.Root;
        if (root == null || root.Name.LocalName != "entities" || root.HasAttributes) return false;
        return existing == null || encoding.GetString(existing).Contains("<entities >");
    }

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
