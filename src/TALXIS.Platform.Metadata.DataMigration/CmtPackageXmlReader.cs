using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// Reads CMT data_schema.xml and data.xml into the typed model. Documents are loaded with whitespace and
/// line information preserved and kept on the <see cref="CmtPackage"/> so <see cref="CmtPackageXmlWriter"/>
/// can patch them in place.
/// </summary>
public sealed class CmtPackageXmlReader
{
    private const LoadOptions XmlLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    /// <summary>
    /// Loads a package from <paramref name="schemaPath"/> and, when it exists, <paramref name="dataPath"/>; a missing
    /// data file leaves <see cref="CmtPackage.Data"/> null. Unreadable or malformed files, and records or associations
    /// whose ids do not parse (those are skipped), are reported in <see cref="CmtPackage.LoadErrors"/>.
    /// </summary>
    /// <param name="schemaPath">Path to data_schema.xml.</param>
    /// <param name="dataPath">Path to data.xml, or <c>null</c> for a schema-only package.</param>
    public CmtPackage Load(string schemaPath, string? dataPath = null)
    {
        var errors = new List<WorkspaceLoadError>();
        var schemaFile = TryLoad(schemaPath, errors);
        var schema = schemaFile == null ? new CmtDataSchema() : ReadSchema(schemaFile.Value.Document, schemaPath);

        CmtData? data = null;
        var dataFile = dataPath == null || !File.Exists(dataPath) ? null : TryLoad(dataPath, errors);
        if (dataFile != null) data = ReadData(dataFile.Value.Document, dataPath, errors);

        return new CmtPackage(schema, data)
        {
            LoadErrors = errors,
            SchemaDocument = schemaFile?.Document,
            SchemaBytes = schemaFile?.Bytes,
            DataDocument = dataFile?.Document,
            DataBytes = dataFile?.Bytes
        };
    }

    /// <summary>
    /// Loads the package in <paramref name="packageDirectory"/> from CMT's fixed file names (<see cref="CmtPackageLayout"/>),
    /// with the same rules as <see cref="Load"/>. The directory is the caller's; nothing is searched for.
    /// </summary>
    /// <param name="packageDirectory">Folder holding data_schema.xml and, optionally, data.xml.</param>
    public CmtPackage LoadDirectory(string packageDirectory)
    {
        return Load(Path.Combine(packageDirectory, CmtPackageLayout.SchemaFileName), Path.Combine(packageDirectory, CmtPackageLayout.DataFileName));
    }

    /// <summary>
    /// Reads a package from already loaded documents. Records or associations whose ids do not parse are
    /// skipped and reported in <see cref="CmtPackage.LoadErrors"/>.
    /// </summary>
    /// <param name="schema">The data_schema.xml document.</param>
    /// <param name="data">The data.xml document, or <c>null</c> for a schema-only package.</param>
    public CmtPackage Read(XDocument schema, XDocument? data)
    {
        var errors = new List<WorkspaceLoadError>();
        var model = data == null ? null : ReadData(data, null, errors);
        return new CmtPackage(ReadSchema(schema), model) { LoadErrors = errors, SchemaDocument = schema, DataDocument = data };
    }

    /// <summary>Reads a data_schema.xml document.</summary>
    /// <param name="document">The document to read.</param>
    /// <param name="sourcePath">File path recorded in each element's <see cref="MetadataBase.Source"/>.</param>
    public CmtDataSchema ReadSchema(XDocument document, string? sourcePath = null)
    {
        var schema = new CmtDataSchema();
        var root = document.Root;
        if (root == null) return schema;

        SetSource(schema, root, sourcePath);
        schema.DateMode = Attr(root, "dateMode");
        foreach (var entity in root.Elements("entity")) schema.Entities.Add(ReadSchemaEntity(entity, sourcePath));
        foreach (var name in root.Elements("entityImportOrder").Elements("entityName")) schema.EntityImportOrder.Add(name.Value);
        return schema;
    }

    // Lenient: "1" or "true" in any case reads as true, anything else as false. CMT accepts only true|false|1|0
    // (case-sensitive); the XSD stage reports other tokens.
    internal static bool ParseBool(string? value) => value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    internal static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

    /// <summary>Parses document bytes the way the reader loads files: whitespace and line information preserved.</summary>
    internal static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream, XmlLoadOptions);
    }

    private static CmtData ReadData(XDocument document, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        var data = new CmtData();
        var root = document.Root;
        if (root == null) return data;

        SetSource(data, root, sourcePath);
        data.Timestamp = Attr(root, "timestamp");
        foreach (var entity in root.Elements("entity")) data.Entities.Add(ReadDataEntity(entity, sourcePath, errors));
        return data;
    }

    private static CmtSchemaEntity ReadSchemaEntity(XElement element, string? sourcePath)
    {
        var entity = new CmtSchemaEntity
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname"),
            PrimaryIdField = Attr(element, "primaryidfield"),
            PrimaryNameField = Attr(element, "primarynamefield"),
            DisablePlugins = BoolOrNull(element, "disableplugins"),
            SkipUpdate = BoolOrNull(element, "skipupdate"),
            ForceCreate = BoolOrNull(element, "forcecreate"),
            RenderLiquid = BoolOrNull(element, "renderliquid"),
            FetchXmlFilter = element.Element("filter")?.Value
        };
        SetSource(entity, element, sourcePath);
        CmtOtherAttributes.Read(element, entity.OtherAttributes, CmtOtherAttributes.SchemaEntity);
        foreach (var field in element.Elements("fields").Elements("field")) entity.Fields.Add(ReadSchemaField(field, sourcePath));
        foreach (var relationship in element.Elements("relationships").Elements("relationship")) entity.Relationships.Add(ReadRelationship(relationship, sourcePath));
        return entity;
    }

    private static CmtSchemaField ReadSchemaField(XElement element, string? sourcePath)
    {
        var field = new CmtSchemaField
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname"),
            Type = Attr(element, "type") ?? string.Empty,
            IsPrimaryKey = Bool(element, "primaryKey"),
            IsUpdateCompare = Bool(element, "updateCompare"),
            IsCustomField = Bool(element, "customfield"),
            LookupType = Attr(element, "lookupType"),
            DateMode = Attr(element, "dateMode")
        };
        SetSource(field, element, sourcePath);
        CmtOtherAttributes.Read(element, field.OtherAttributes, CmtOtherAttributes.SchemaField);
        return field;
    }

    private static CmtSchemaRelationship ReadRelationship(XElement element, string? sourcePath)
    {
        var relationship = new CmtSchemaRelationship
        {
            Name = Attr(element, "name") ?? string.Empty,
            IsManyToMany = Bool(element, "manyToMany"),
            IsReflexive = BoolOrNull(element, "isreflexive"),
            RelatedEntityName = Attr(element, "relatedEntityName"),
            M2mTargetEntity = Attr(element, "m2mTargetEntity"),
            M2mTargetEntityPrimaryKey = Attr(element, "m2mTargetEntityPrimaryKey"),
            ReferencingAttribute = Attr(element, "referencingAttribute"),
            ReferencedEntity = Attr(element, "referencedEntity"),
            ReferencedAttribute = Attr(element, "referencedAttribute"),
            ReferencingEntity = Attr(element, "referencingEntity")
        };
        SetSource(relationship, element, sourcePath);
        CmtOtherAttributes.Read(element, relationship.OtherAttributes, CmtOtherAttributes.Relationship);
        foreach (var field in element.Elements("fields").Elements("field")) relationship.Fields.Add(ReadSchemaField(field, sourcePath));
        return relationship;
    }

    private static CmtDataEntity ReadDataEntity(XElement element, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        var entity = new CmtDataEntity
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname")
        };
        SetSource(entity, element, sourcePath);
        CmtOtherAttributes.Read(element, entity.OtherAttributes, CmtOtherAttributes.DataEntity);
        foreach (var recordElement in element.Elements("records").Elements("record"))
        {
            var record = ReadRecord(recordElement, sourcePath, errors);
            if (record != null) entity.Records.Add(record);
        }
        foreach (var m2mElement in element.Elements("m2mrelationships").Elements("m2mrelationship"))
        {
            var m2m = ReadManyToMany(m2mElement, sourcePath, errors);
            if (m2m != null) entity.ManyToManyRelationships.Add(m2m);
        }
        return entity;
    }

    // A record whose id does not parse is skipped and reported: silently mapping it to Guid.Empty would
    // make the writer rewrite the attribute and the validators match the wrong record. Activity parties are the
    // exception: CMT imports them without an id, so an absent one reads as Guid.Empty and is never written.
    private static CmtDataRecord? ReadRecord(XElement element, string? sourcePath, List<WorkspaceLoadError> errors, bool isParty = false)
    {
        var idText = Attr(element, "id");
        var id = isParty && idText == null ? Guid.Empty : ParseGuid(idText);
        if (id == null)
        {
            errors.Add(LoadError(element, sourcePath, $"<{element.Name.LocalName}> has no valid GUID in its id attribute ('{idText}'); the record is skipped."));
            return null;
        }

        var record = new CmtDataRecord { Id = id.Value, NewId = ParseGuid(Attr(element, "newId")) };
        SetSource(record, element, sourcePath);
        CmtOtherAttributes.Read(element, record.OtherAttributes, CmtOtherAttributes.Record);
        foreach (var fieldElement in element.Elements("field"))
        {
            var field = new CmtDataField
            {
                Name = Attr(fieldElement, "name") ?? string.Empty,
                Value = Attr(fieldElement, "value"),
                FileName = Attr(fieldElement, "filename"),
                LookupEntity = Attr(fieldElement, "lookupentity"),
                LookupEntityName = Attr(fieldElement, "lookupentityname")
            };
            SetSource(field, fieldElement, sourcePath);
            CmtOtherAttributes.Read(fieldElement, field.OtherAttributes, CmtOtherAttributes.DataField);
            // Partylist: one <activitypointerrecords> element per activity party, directly under the field.
            foreach (var partyElement in fieldElement.Elements("activitypointerrecords"))
            {
                var party = ReadRecord(partyElement, sourcePath, errors, isParty: true);
                if (party != null) field.ActivityPointerRecords.Add(party);
            }
            record.Fields.Add(field);
        }
        return record;
    }

    private static CmtDataManyToManyRelationship? ReadManyToMany(XElement element, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        var sourceIdText = Attr(element, "sourceid");
        var sourceId = ParseGuid(sourceIdText);
        if (sourceId == null)
        {
            errors.Add(LoadError(element, sourcePath, $"<m2mrelationship> has no valid GUID in its sourceid attribute ('{sourceIdText}'); the association is skipped."));
            return null;
        }

        var m2m = new CmtDataManyToManyRelationship
        {
            SourceId = sourceId.Value,
            TargetEntityName = Attr(element, "targetentityname") ?? string.Empty,
            TargetEntityNameIdField = Attr(element, "targetentitynameidfield"),
            RelationshipName = Attr(element, "m2mrelationshipname") ?? string.Empty,
            RelationshipSchemaName = Attr(element, "m2mrelationshipschemaname")
        };
        SetSource(m2m, element, sourcePath);
        CmtOtherAttributes.Read(element, m2m.OtherAttributes, CmtOtherAttributes.ManyToMany);
        foreach (var target in element.Elements("targetids").Elements("targetid"))
        {
            var targetId = ParseGuid(target.Value);
            if (targetId != null)
                m2m.TargetIds.Add(targetId.Value);
            else
                errors.Add(LoadError(target, sourcePath, $"<targetid> '{target.Value}' is not a GUID; the target is skipped."));
        }
        return m2m;
    }

    private static (XDocument Document, byte[] Bytes)? TryLoad(string path, List<WorkspaceLoadError> errors)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return (Parse(bytes), bytes);
        }
        catch (XmlException ex)
        {
            errors.Add(new WorkspaceLoadError(path, ex.Message, ex.LineNumber, ex.LinePosition));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new WorkspaceLoadError(path, ex.Message));
        }
        return null;
    }

    private static WorkspaceLoadError LoadError(XElement element, string? sourcePath, string message)
    {
        var line = (IXmlLineInfo)element;
        return line.HasLineInfo()
            ? new WorkspaceLoadError(sourcePath ?? string.Empty, message, line.LineNumber, line.LinePosition)
            : new WorkspaceLoadError(sourcePath ?? string.Empty, message);
    }

    private static void SetSource(MetadataBase model, XElement element, string? sourcePath)
    {
        var line = (IXmlLineInfo)element;
        if (line.HasLineInfo()) model.Source = new SourceLocation(sourcePath ?? string.Empty, line.LineNumber, line.LinePosition);
    }

    private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

    private static bool Bool(XElement element, string name) => BoolOrNull(element, name) == true;

    private static bool? BoolOrNull(XElement element, string name)
    {
        var value = Attr(element, name);
        return value == null ? null : ParseBool(value);
    }
}
