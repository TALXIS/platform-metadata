using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// Reads CMT data_schema.xml and data.xml into the typed model. Documents are loaded with whitespace and
/// line information preserved and kept on the <see cref="CmtPackage"/> so <see cref="CmtPackageXmlWriter"/>
/// can patch them in place.
/// </summary>
public sealed class CmtPackageXmlReader
{
    private const LoadOptions XmlLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    /// <summary>
    /// Loads a package from explicit file paths (<paramref name="dataPath"/> <c>null</c> for a schema-only package).
    /// Missing or malformed files, and records or associations whose ids do not parse (those are skipped), are
    /// reported in <see cref="CmtPackage.LoadErrors"/>. A single path means a package directory: see <see cref="Load(string)"/>.
    /// </summary>
    public CmtPackage Load(string schemaPath, string? dataPath)
    {
        var errors = new List<WorkspaceLoadError>();
        var schemaFile = TryLoad(schemaPath, errors);
        var schema = schemaFile == null ? new CmtDataSchema() : ReadSchema(schemaFile.Value.Document, schemaPath);

        CmtData? data = null;
        var dataFile = dataPath == null ? null : TryLoad(dataPath, errors);
        if (dataFile != null) data = ReadData(dataFile.Value.Document, dataPath, errors);

        return new CmtPackage(schema, data, errors)
        {
            SchemaDocument = schemaFile?.Document,
            SchemaBytes = schemaFile?.Bytes,
            DataDocument = dataFile?.Document,
            DataBytes = dataFile?.Bytes
        };
    }

    /// <summary>
    /// Loads the package in <paramref name="packageDirectory"/>: <see cref="CmtPackageLayout.SchemaFileName"/> and,
    /// when present, <see cref="CmtPackageLayout.DataFileName"/> (a missing data file leaves <see cref="CmtPackage.Data"/>
    /// null without a load error). The directory is the caller's; nothing is searched for.
    /// </summary>
    public CmtPackage Load(string packageDirectory)
    {
        var dataPath = Path.Combine(packageDirectory, CmtPackageLayout.DataFileName);
        return Load(Path.Combine(packageDirectory, CmtPackageLayout.SchemaFileName), FileExists(dataPath) ? dataPath : null);
    }

    /// <summary>
    /// Reads a package from already loaded documents. Records or associations whose ids do not parse are
    /// skipped and reported in <see cref="CmtPackage.LoadErrors"/>.
    /// </summary>
    public CmtPackage Read(XDocument schema, XDocument? data)
    {
        var errors = new List<WorkspaceLoadError>();
        return new CmtPackage(ReadSchema(schema), data is null ? null : ReadData(data, null, errors), errors) { SchemaDocument = schema, DataDocument = data };
    }

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
    /// Reads a data.xml document. Records or associations whose ids do not parse are skipped; use
    /// <see cref="Read"/> or <see cref="Load"/> to receive them as load errors.
    /// </summary>
    public CmtData ReadData(XDocument document, string? sourcePath = null) => ReadData(document, sourcePath, new List<WorkspaceLoadError>());

    private static CmtData ReadData(XDocument document, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        var data = new CmtData();
        var root = document.Root;
        if (root is null) return data;

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
            Type = Attr(element, "type") ?? string.Empty,
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

    private static CmtDataEntity ReadDataEntity(XElement element, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        var entity = new CmtDataEntity
        {
            Name = Attr(element, "name") ?? string.Empty,
            DisplayName = Attr(element, "displayname")
        };
        SetSource(entity, element, sourcePath);
        foreach (var record in element.Elements("records").Elements("record"))
        {
            if (ReadRecord(record, sourcePath, errors) is { } model) entity.Records.Add(model);
        }
        foreach (var m2m in element.Elements("m2mrelationships").Elements("m2mrelationship"))
        {
            if (ReadManyToMany(m2m, sourcePath, errors) is { } model) entity.ManyToManyRelationships.Add(model);
        }
        return entity;
    }

    // A record whose id does not parse is skipped and reported: silently mapping it to Guid.Empty would
    // make the writer rewrite the attribute and the validators match the wrong record. Activity parties are the
    // exception: CMT imports them without an id, so an absent one reads as Guid.Empty and is never written.
    private static CmtDataRecord? ReadRecord(XElement element, string? sourcePath, List<WorkspaceLoadError> errors, bool isParty = false)
    {
        var idText = Attr(element, "id");
        if ((isParty && idText is null ? Guid.Empty : CmtXml.ParseGuid(idText)) is not { } id)
        {
            errors.Add(LoadError(element, sourcePath, $"<{element.Name.LocalName}> has no valid GUID in its id attribute ('{idText}'); the record is skipped."));
            return null;
        }

        var newId = Attr(element, "newId");
        var record = new CmtDataRecord { Id = id, NewId = newId is null ? null : CmtXml.ParseGuid(newId) };
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
            // Partylist: CMT writes one <activitypointerrecords id="…"> element per activity party, directly under the field.
            foreach (var party in fieldElement.Elements("activitypointerrecords"))
            {
                if (ReadRecord(party, sourcePath, errors, isParty: true) is { } model) field.ActivityPointerRecords.Add(model);
            }
            record.Fields.Add(field);
        }
        return record;
    }

    private static CmtDataManyToManyRelationship? ReadManyToMany(XElement element, string? sourcePath, List<WorkspaceLoadError> errors)
    {
        if (CmtXml.ParseGuid(Attr(element, "sourceid")) is not { } sourceId)
        {
            errors.Add(LoadError(element, sourcePath, $"<m2mrelationship> has no valid GUID in its sourceid attribute ('{Attr(element, "sourceid")}'); the association is skipped."));
            return null;
        }

        var m2m = new CmtDataManyToManyRelationship
        {
            SourceId = sourceId,
            TargetEntityName = Attr(element, "targetentityname") ?? string.Empty,
            TargetEntityNameIdField = Attr(element, "targetentitynameidfield"),
            RelationshipName = Attr(element, "m2mrelationshipname") ?? string.Empty,
            RelationshipSchemaName = Attr(element, "m2mrelationshipschemaname")
        };
        SetSource(m2m, element, sourcePath);
        foreach (var target in element.Elements("targetids").Elements("targetid"))
        {
            if (CmtXml.ParseGuid(target.Value) is { } targetId) m2m.TargetIds.Add(targetId);
            else errors.Add(LoadError(target, sourcePath, $"<targetid> '{target.Value}' is not a GUID; the target is skipped."));
        }
        return m2m;
    }

    /// <summary>Parses document bytes the way the reader loads files: whitespace and line information preserved.</summary>
    internal static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream, XmlLoadOptions);
    }

    private static (XDocument Document, byte[] Bytes)? TryLoad(string path, List<WorkspaceLoadError> errors)
    {
        try
        {
            var bytes = ReadAllBytes(path);
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

    // TODO(layering): route through IWorkspaceContext once the metamodel-layering branch lands; these are the reader's only file accesses.
    private static byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    private static bool FileExists(string path) => File.Exists(path);

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

    private static bool? BoolOrNull(XElement element, string name) =>
        Attr(element, name) is { } value ? CmtXml.ParseBool(value) : null;
}
