using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Reads CMT data_schema.xml and data.xml into the typed model.
/// </summary>
public sealed class CmtPackageXmlReader
{
    private const LoadOptions XmlLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    /// <summary>
    /// Loads a package from files. Missing or malformed files are reported in LoadErrors.
    /// </summary>
    public CmtPackage Load(string schemaPath, string? dataPath = null)
    {
        var errors = new List<WorkspaceLoadError>();
        var schemaDocument = TryLoad(schemaPath, errors);
        var schema = schemaDocument is null ? new CmtDataSchema() : ReadSchema(schemaDocument, schemaPath);

        CmtData? data = null;
        XDocument? dataDocument = null;
        if (dataPath is not null)
        {
            dataDocument = TryLoad(dataPath, errors);
            if (dataDocument is not null) data = ReadData(dataDocument, dataPath);
        }

        return new CmtPackage(schema, data, errors) { SchemaDocument = schemaDocument, DataDocument = dataDocument };
    }

    /// <summary>
    /// Reads a package from already loaded documents.
    /// </summary>
    public CmtPackage Read(XDocument schema, XDocument? data) =>
        new(ReadSchema(schema), data is null ? null : ReadData(data)) { SchemaDocument = schema, DataDocument = data };

    /// <summary>
    /// Reads a data_schema.xml document.
    /// </summary>
    public CmtDataSchema ReadSchema(XDocument document, string? sourcePath = null)
    {
        var schema = new CmtDataSchema();
        var root = document.Root;
        if (root is null) return schema;

        SetSource(schema, root, sourcePath);
        schema.DateMode = Attr(root, "dateMode");
        foreach (var entity in root.Elements("entity")) schema.Entities.Add(ReadSchemaEntity(entity, sourcePath));
        foreach (var name in root.Elements("entityImportOrder").Elements("entityName")) schema.EntityImportOrder.Add(name.Value);
        return schema;
    }

    /// <summary>
    /// Reads a data.xml document.
    /// </summary>
    public CmtData ReadData(XDocument document, string? sourcePath = null)
    {
        var data = new CmtData();
        var root = document.Root;
        if (root is null) return data;

        SetSource(data, root, sourcePath);
        data.Timestamp = Attr(root, "timestamp");
        foreach (var entity in root.Elements("entity")) data.Entities.Add(ReadDataEntity(entity, sourcePath));
        return data;
    }

    private static CmtSchemaEntity ReadSchemaEntity(XElement element, string? sourcePath)
    {
        var entity = new CmtSchemaEntity
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname"),
            ObjectTypeCode = int.TryParse(Attr(element, "etc"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var etc) ? etc : null,
            PrimaryIdField = Attr(element, "primaryidfield"),
            PrimaryNameField = Attr(element, "primarynamefield"),
            DisablePlugins = BoolOrNull(element, "disableplugins"),
            SkipUpdate = BoolOrNull(element, "skipupdate"),
            ForceCreate = BoolOrNull(element, "forcecreate"),
            RenderLiquid = BoolOrNull(element, "renderliquid"),
            GuidSwap = BoolOrNull(element, "guidswap"),
            FetchXmlFilter = element.Element("filter")?.Value
        };
        SetSource(entity, element, sourcePath);
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
            Type = Attr(element, "type"),
            IsPrimaryKey = Bool(element, "primaryKey"),
            IsUpdateCompare = Bool(element, "updateCompare"),
            IsCustomField = Bool(element, "customfield"),
            LookupType = Attr(element, "lookupType"),
            DateMode = Attr(element, "dateMode")
        };
        SetSource(field, element, sourcePath);
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
        foreach (var field in element.Elements("fields").Elements("field")) relationship.Fields.Add(ReadSchemaField(field, sourcePath));
        return relationship;
    }

    private static CmtDataEntity ReadDataEntity(XElement element, string? sourcePath)
    {
        var entity = new CmtDataEntity
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname")
        };
        SetSource(entity, element, sourcePath);
        foreach (var record in element.Elements("records").Elements("record")) entity.Records.Add(ReadRecord(record, sourcePath));
        foreach (var m2m in element.Elements("m2mrelationships").Elements("m2mrelationship")) entity.ManyToManyRelationships.Add(ReadManyToMany(m2m, sourcePath));
        return entity;
    }

    private static CmtDataRecord ReadRecord(XElement element, string? sourcePath)
    {
        var newId = Attr(element, "newId");
        var record = new CmtDataRecord
        {
            Id = ParseGuid(Attr(element, "id")),
            NewId = newId is null ? null : ParseGuid(newId)
        };
        SetSource(record, element, sourcePath);
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
            // Partylist: every child of <activitypointerrecords> is an activity party record, whatever CMT names it.
            foreach (var party in fieldElement.Elements("activitypointerrecords").Elements()) field.ActivityPointerRecords.Add(ReadRecord(party, sourcePath));
            record.Fields.Add(field);
        }
        return record;
    }

    private static CmtDataManyToManyRelationship ReadManyToMany(XElement element, string? sourcePath)
    {
        var m2m = new CmtDataManyToManyRelationship
        {
            SourceId = ParseGuid(Attr(element, "sourceid")),
            TargetEntityName = Attr(element, "targetentityname") ?? string.Empty,
            TargetEntityNameIdField = Attr(element, "targetentitynameidfield"),
            RelationshipName = Attr(element, "m2mrelationshipname") ?? string.Empty,
            RelationshipSchemaName = Attr(element, "m2mrelationshipschemaname")
        };
        SetSource(m2m, element, sourcePath);
        foreach (var target in element.Elements("targetids").Elements("targetid")) m2m.TargetIds.Add(ParseGuid(target.Value));
        return m2m;
    }

    private static XDocument? TryLoad(string path, List<WorkspaceLoadError> errors)
    {
        try
        {
            return XDocument.Load(path, XmlLoadOptions);
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

    private static void SetSource(MetadataBase model, XElement element, string? sourcePath)
    {
        var line = (IXmlLineInfo)element;
        if (line.HasLineInfo()) model.Source = new SourceLocation(sourcePath ?? string.Empty, line.LineNumber, line.LinePosition);
    }

    private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

    private static bool Bool(XElement element, string name) => BoolOrNull(element, name) == true;

    // XmlSerializer lexical forms: true|false|1|0. Anything else is treated as false but preserved by the writer.
    private static bool? BoolOrNull(XElement element, string name)
    {
        var value = Attr(element, name);
        if (value is null) return null;
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : Guid.Empty;
}
