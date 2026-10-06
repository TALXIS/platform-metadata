# Configuration Migration packages

A Configuration Migration Tool (CMT) package moves configuration data between environments: `data_schema.xml` says which tables and columns to migrate and how records are matched, `data.xml` holds the records. Microsoft CMT, Package Deployer and `pac data` read the pair from the root of a zip; the TALXIS importer (INT0014-DataMovement) reads the same files with a looser contract and two extra attributes. Packages are source-controlled next to solutions, so this library models them like any other component: typed, roundtrip-safe, validated.

Nothing here touches Dataverse, discovers files or bakes in folder names. `CmtPackageLayout` names the files CMT expects (`data_schema.xml`, `data.xml`, `files/`); locating a package is the consumer's job (build tasks, txc). `[Content_Types].xml` and TALXIS's `guids.json` are zip-level artifacts and not part of the model.

## Model

`TALXIS.Platform.Metadata.ConfigurationMigration` (core package, no dependencies) mirrors the two files element by element: `CmtDataSchema` with its entities, fields and relationships, and `CmtData` with its records, fields and many-to-many associations. Every type derives from `MetadataBase` and carries a `Source` location. `FindEntity` and `FindField` compare names ordinally, as CMT's importer does; a name that matches only when letter case is ignored is reported as TXM015.

## Create, load, change, save

`TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration`. Creating a package:

```csharp
var schema = new CmtDataSchema();
var account = schema.AddEntity("account", primaryIdField: "accountid", primaryNameField: "name", displayName: "Account");
account.AddField("name", CmtFieldTypes.String, "Account Name", updateCompare: true);
account.AddField("telephone1", CmtFieldTypes.String, "Main Phone");
account.AddField("primarycontactid", CmtFieldTypes.EntityReference, "Primary Contact", lookupType: "contact");

var data = new CmtData();
// Configuration records need stable ids: commit a fixed GUID (or use the TALXIS guidswap extension), never Guid.NewGuid() per run.
data.AddEntity("account", "Account")
    .AddRecord(new Guid("5f1c2a40-7b3e-4c6d-9e8f-0a1b2c3d4e5f"))
    .Set("name", "Contoso")
    .Set("telephone1", "123456789");

new CmtPackageXmlWriter().Save(new CmtPackage(schema, data), @"C:\MyPackage");   // writes data_schema.xml and data.xml
```

Editing an existing one:

```csharp
var package = new CmtPackageXmlReader().LoadDirectory(@"C:\MyPackage");
// package.LoadErrors lists unreadable files and records whose id is not a GUID (those are skipped).

var account = package.Schema.FindEntity("account")!;
account.FindField("telephone1")!.IsUpdateCompare = true;
account.AddField("websiteurl", CmtFieldTypes.String, "Website");

var changed = new CmtPackageXmlWriter().Save(package, @"C:\MyPackage");   // false when nothing changed
```

`LoadDirectory(directory)`/`Save(package, directory)` combine the directory with CMT's fixed file names (`CmtPackageLayout`); `Load(schemaPath, dataPath)`, `SaveSchema` and `SaveData` take file paths. A missing data file leaves `Data` null in both loaders. Object initialisers keep working (`new CmtSchemaField { Name = …, Type = … }`); the helpers only add duplicate checks and the conventions CMT expects (a `guid` primary-key field, the import-order entry).

The writer does not regenerate files. It patches the documents the package was loaded from, matching elements by name (entities, fields, relationships) or id (records, associations), so unknown attributes and elements, XML comments, attribute order, BOM, line endings, the declaration and CMT's `<entities >` root survive, and the diff contains only the intended change. `Load → Save` is a zero-byte diff; an unchanged document is never re-serialised. Elements are written in CMT's order (`entityImportOrder` after the entities; `fields`, `relationships`, `filter` inside an entity). A package built in memory (`new CmtPackage(schema, data)`) is written as a new, indented document.

## Validation

Two validators in `TALXIS.Platform.Metadata.Validation`, run by `WorkspaceValidator.ValidateDirectory` in stage `CmtData`, plus the XSD `CmtData.xsd` in the schema stage:

- `CmtDataSchemaValidator.Validate(CmtDataSchema)` (also `ValidateFile`/`ValidateXml`): the schema on its own. Runs on every `data_schema.xml` in the workspace.
- `CmtPackageValidator.Validate(CmtPackage)`: data.xml against its schema. Runs only when a `data.xml` sits next to the schema and both files load without errors; load errors are left to the XSD stage.
- `WorkspaceValidator` itself checks `files/` payloads (TXM023) under the same condition; it is the one rule that needs the package folder.

| Code | Rule | Severity | What CMT does |
|---|---|---|---|
| TXM006 | entity has no `updateCompare="true"` field | error without `primarynamefield`, else warning | matches existing records on the primary name and overwrites them, unrelated same-named records included; with neither, every re-import duplicates the records |
| TXM007 | `entityImportOrder` names an undeclared entity / a declared entity is missing from it | warning | ignores unknown names; lookups to later records are deferred to a second pass |
| TXM008 | `primaryidfield` missing, undeclared or not `guid` / not marked `primaryKey` | error / warning | creates the records but the second-pass update fails (self lookups lost); the flag itself is not needed |
| TXM009 | `primarynamefield` not declared | warning | uses it as the matching fallback; TALXIS ignores it |
| TXM011 | duplicate entity or field name (case-insensitive) | error | TALXIS throws on its case-insensitive dictionary |
| TXM012 | data.xml entity or field not declared in the schema | error | skips it with a log warning and exits 0; TALXIS throws |
| TXM013 | record `lookupentity` not declared in the package (`systemuser`, `team`, `businessunit`, `transactioncurrency`, `organization` excepted) | warning | the target must already exist in the environment |
| TXM014 | M2M relationship not declared on the entity / target entity not in the package | error / warning | crashes after the records are committed; targets may come from another package |
| TXM015 | name in the wrong letter case (see below) | error / warning | see below |
| TXM016 | field type missing, not in the vocabulary, not lowercase, `customer`, `unknown` / `bigint`, TALXIS `file` | error / warning | rejects the package (`customer` columns are `entityreference`) / drops `bigint` values; rejects `file` |
| TXM017 | record's primary-id field value empty, not a GUID or different from `record@id`, or a repeated `record@id` / the field absent | error / warning | creates the record under the field value or under a new id; skips lookups to a repeated id |
| TXM018 | `dateMode` not `absolute`/`relative`/`relativeDaily` | error | cannot deserialise the schema |
| TXM019 | data.xml `timestamp` does not parse | error | aborts the import |
| TXM020 | `<filter>` is not FetchXML with a `<fetch>` root | warning | ignored on import; the CMT GUI fails to open it |
| TXM021 | lookup value without `lookupentity` or `lookupentityname`, or with a `lookupentity` outside the field's `lookupType` (a case-only difference is TXM015) | warning | skips the lookup, exits 0 |
| TXM022 | value not in the text form CMT reads for its type (see below), or a number with a thousands separator | warning | drops, zeroes or misreads it, exits 0; `1,234` parses per the importing machine's culture |
| TXM023 | `filedata` value without `files/<value>.bin` in the package folder | warning | fails that record, exits 0 |
| TXM024 | M2M `targetentitynameidfield` is not the target entity's `primaryidfield` | error | crashes after the records are committed |
| TXM025 | two records of an entity share their `updateCompare` values (or primary name when there are none) | warning | on re-import every one of them updates the same existing record |

TXM015 in detail, because CMT compares every name ordinally:

- **Errors:** a schema entity or field name that is not lowercase (CMT rejects the package); a data.xml entity that matches only when case is ignored (the import fails); a `lookupentity` that differs only by case (CMT fails to insert the record); an `m2mrelationshipname` that differs only by case (CMT crashes after the records are committed).
- **Warnings:** a data.xml field (CMT drops its values), an `entityImportOrder` name or an M2M target entity that differs only by case.

### Value encodings (`CmtDataField.Value`)

- **bool:** `true|false` in any case. CMT writes `True|False` and reads `1`, `0`, `yes`, `t` and `f` as false.
- **number, decimal, float, money:** invariant, without currency symbols or thousands separators. `number` and `optionsetvalue` take integers only: `42.0` and `71000000.5` are dropped.
- **datetime:** invariant round-trip (`2026-01-01T00:00:00.0000000`, unspecified kind = UTC); CMT writes `Z` for user-local columns.
- **guid:** the GUID. **optionsetvalue:** the integer.
- **optionsetvaluecollection:** comma-separated integers or the form CMT exports, `[-1,71000010,71000012,-1]`; both import. `[-1,-1]` is empty; `[a,b]` and `a;b` are dropped.
- **string:** HTML-encoded; CMT decodes once on import, TALXIS does not. Multi-line text is entitised (`&#xD;&#xA;`) and preserved as such.
- **imagedata:** base64. **filedata:** the file id, with `FileName` as display name and the payload at `files/<id>.bin`.
- **Lookups:** the GUID plus `LookupEntity` and `LookupEntityName`; CMT needs both.
- **partylist:** an empty value plus one `<activitypointerrecords id="...">` element per party directly under the field (`ActivityPointerRecords`): the activitypartyid as id, the `partyid` lookup, `participationtypemask` and the other party columns as fields. CMT also imports parties without an id.

### TALXIS dialect

The TALXIS importer reads only `name`, `primaryidfield`, `skipupdate`, `renderliquid`, `guidswap` on entities and `name`/`type` on fields; it ignores `etc`, `displayname` (packages use `#`), `primarynamefield`, `disableplugins`, `primaryKey`, `customfield`, `updateCompare`, `lookupType`, `entityImportOrder`, `<relationships>`, `dateMode`, `filter`, `timestamp` and `lookupentityname`. It matches records by `record@id` (upsert, `skipupdate` = create-only), applies lookups in a second pass so import order does not matter, reads M2M from data.xml, resolves `type="file"` values by substring match over zip entries, and declares XML with `<?xml version="1.0"?>` and no encoding. `renderliquid="true"` renders the entity's `<records>` through DotLiquid before import; `guidswap="true"` remaps ids consistently. Where the two importers give an attribute different force, the rule above is a warning, never an error, so a TALXIS package validates without errors.

### Deliberately not validated

Liquid syntax in values (runtime templates are normal data), `displayname` content, schema entities without data, a missing `<m2mrelationships>`, `lookupentityname` content (any non-empty name works), double-encoded text, and anything that needs Dataverse metadata (entity/column existence, type agreement, option values, whether a lookup target exists). Metadata-aware rules are tracked in #124. A declaration that says `utf-16` over UTF-8 bytes stays a load error although CMT's reader tolerates it.

### Limits

TXM007's "undeclared name" finding points at the schema file's root (the import order is a list of strings without positions). Hand-edited formatting that `XDocument` cannot represent (attributes split over lines, `/>` without a space) is kept as long as the document is unchanged and normalised on its first real edit.
