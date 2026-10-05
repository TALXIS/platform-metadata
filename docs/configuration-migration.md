# Configuration Migration packages

A Configuration Migration Tool (CMT) package moves configuration data between environments: `data_schema.xml` says which tables and columns to migrate and how records are matched, `data.xml` holds the records. Microsoft CMT, Package Deployer and `pac data` read the pair from the root of a zip; the TALXIS importer (INT0014-DataMovement) reads the same files with a looser contract and two extra attributes. Packages are source-controlled next to solutions, so this library models them like any other component: typed, roundtrip-safe, validated.

Nothing here touches Dataverse, discovers files or bakes in folder names. `CmtPackageLayout` names the files CMT expects (`data_schema.xml`, `data.xml`, `files/`); locating a package is the consumer's job (build tasks, txc). `[Content_Types].xml` and TALXIS's `guids.json` are zip-level artifacts and not part of the model.

## Model

`TALXIS.Platform.Metadata.ConfigurationMigration` (core package, no dependencies, every type derives from `MetadataBase` and carries a `Source` location):

| Type | What it is |
|---|---|
| `CmtDataSchema` | root of data_schema.xml: `DateMode`, `EntityImportOrder`, `Entities`, `FindEntity` (ordinal) |
| `CmtSchemaEntity` | a table: `Name`, `DisplayName`, `ObjectTypeCode` (`etc`), `PrimaryIdField`, `PrimaryNameField`, `DisablePlugins`/`SkipUpdate`/`ForceCreate` (`bool?`, absent = null), `RenderLiquid`/`GuidSwap` (TALXIS), `FetchXmlFilter`, `Fields`, `Relationships`, `FindField` (ordinal) |
| `CmtSchemaField` | a column: `Name`, `DisplayName`, `Type` (lowercase CMT vocabulary), `IsPrimaryKey`/`IsUpdateCompare`/`IsCustomField` (absent = false, false is never written), `LookupType` (`account\|contact` or `*`), `DateMode` |
| `CmtSchemaRelationship` | N:1 (`Referenc*`) or M2M (`M2m*`, nested intersect `Fields`) entry |
| `CmtData` | root of data.xml: `Timestamp` (raw text), `Entities`, `FindEntity` |
| `CmtDataEntity` | records and `ManyToManyRelationships` of one table |
| `CmtDataRecord` | `Id`, `NewId`, `Fields` |
| `CmtDataField` | `Name`, `Value` (CMT-encoded text), `FileName`, `LookupEntity`, `LookupEntityName`, `ActivityPointerRecords` (partylist) |
| `CmtDataManyToManyRelationship` | `SourceId`, `TargetEntityName`, `TargetEntityNameIdField`, `RelationshipName`, `RelationshipSchemaName`, `TargetIds` |
| `CmtFieldTypes`, `CmtDateModes`, `CmtPackageLayout` | constants; `CmtFieldTypes.Importable` is the set CMT can import |
| `CmtFieldTypeMapper` | `AttributeType` → CMT type, as CMT's generator maps it (Customer → `entityreference`); `null` where CMT has no type |

Names are compared **ordinally** everywhere, because that is what CMT's importer does. A name that matches only when case is ignored is a validation warning (TXM015), not a match.

## Load, change, save

`TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration`:

```csharp
var reader = new CmtPackageXmlReader();
var package = reader.Load(@"C:\MyPackage\data_schema.xml", @"C:\MyPackage\data.xml");
// package.LoadErrors lists unreadable files and records whose id is not a GUID (those are skipped).

var account = package.Schema.FindEntity("account")!;
account.FindField("telephone1")!.IsUpdateCompare = true;
account.Fields.Add(new CmtSchemaField { Name = "websiteurl", DisplayName = "Website", Type = CmtFieldTypes.String });

var writer = new CmtPackageXmlWriter();
writer.SaveSchema(package, @"C:\MyPackage\data_schema.xml");
writer.SaveData(package, @"C:\MyPackage\data.xml");
```

The writer does not regenerate files. It patches the documents the package was loaded from, matching elements by name (entities, fields, relationships) or id (records, associations), so unknown attributes and elements, XML comments, attribute order, BOM, line endings, the declaration and CMT's `<entities >` root survive, and the diff contains only the intended change. `Load → Save` is a zero-byte diff; an unchanged document is never re-serialised. Elements are written in CMT's order (`entityImportOrder` after the entities; `fields`, `relationships`, `filter` inside an entity). A package built in memory (`new CmtPackage(schema, data)`) is written as a new, indented document.

## Validation

Two validators in `TALXIS.Platform.Metadata.Validation`, both run by `WorkspaceValidator.ValidateDirectory` for every `data_schema.xml` in a workspace (stage `CmtData`), plus the XSD `CmtData.xsd` in the schema stage:

- `CmtDataSchemaValidator.Validate(CmtDataSchema)` (also `ValidateFile`/`ValidateXml`): the schema on its own.
- `CmtPackageValidator.Validate(CmtPackage)`: data.xml against its schema.

| Code | Rule | Severity | Why |
|---|---|---|---|
| TXM006 | entity has no `updateCompare="true"` field | error | imports cannot match existing records and duplicate data |
| TXM007 | `entityImportOrder` names an undeclared entity / a declared entity is missing from it | error / warning | CMT skips unknown names; missing entities get an undefined position |
| TXM008 | `primaryidfield` missing, undeclared, not `primaryKey`, not `guid` | error | records cannot be identified |
| TXM009 | `primarynamefield` not declared | warning | CMT uses it as a matching fallback; TALXIS ignores it |
| TXM010 | `entityreference`/`customer` field without `lookupType` | warning | schema does not say where the lookup points; `owner` never carries one |
| TXM011 | duplicate entity or field name (case-insensitive) | error | TALXIS throws on its case-insensitive dictionary |
| TXM012 | data.xml entity or field not declared in the schema | error | CMT silently drops it, TALXIS throws |
| TXM013 | record `lookupentity` not declared in the package | warning | target may exist in the environment already |
| TXM014 | M2M relationship not declared on the entity / target entity not in the package | error / warning | TALXIS reads M2M from data.xml, targets may be external |
| TXM015 | a name matches a declaration only when case is ignored | warning | CMT compares ordinally; the import will not find it |
| TXM016 | field type not importable: unknown name, not lowercase, `bigint`, `unknown` | error (`file` synonym: warning) | CMT has no conversion for it |
| TXM017 | entity `displayname`/`etc`/`disableplugins` or field `displayname` absent | warning | CMT's schema requires them, TALXIS omits them |
| TXM018 | `dateMode` not `absolute`/`relative`/`relativeDaily` | error | CMT cannot deserialise the schema |
| TXM019 | data.xml `timestamp` does not parse | error | CMT aborts the import |
| TXM020 | `<filter>` is not FetchXML with a `<fetch>` root | warning | export-only; the CMT GUI fails to open it |

Rules are derived from the importers' observed behaviour; no decompiled code is used.

### Value encodings (`CmtDataField.Value`)

bool `true|false` (CMT also writes `True|False`); numbers invariant, commas stripped, money may carry a currency symbol; datetime invariant round-trip (`2026-01-01T00:00:00.0000000`, unspecified kind = UTC); guid; `optionsetvalue` the integer; `optionsetvaluecollection` comma-separated integers (TALXIS writes a JSON array, both parse); `string` HTML-encoded (CMT decodes once on import, TALXIS does not); `imagedata` base64; `filedata` the file id with `FileName` as display name and the payload at `files/<id>.bin`; lookups the GUID plus `LookupEntity`/`LookupEntityName`; `partylist` nested `ActivityPointerRecords` instead of a value. Multi-line text is entitised (`&#xD;&#xA;`) and preserved as such.

### TALXIS dialect

The TALXIS importer reads only `name`, `primaryidfield`, `skipupdate`, `renderliquid`, `guidswap` on entities and `name`/`type` on fields; it ignores `etc`, `displayname` (packages use `#`), `primarynamefield`, `disableplugins`, `primaryKey`, `customfield`, `updateCompare`, `lookupType`, `entityImportOrder`, `<relationships>`, `dateMode`, `filter`, `timestamp` and `lookupentityname`. It matches records by `record@id` (upsert, `skipupdate` = create-only), applies lookups in a second pass so import order does not matter, reads M2M from data.xml, resolves `type="file"` values by substring match over zip entries, and declares XML with `<?xml version="1.0"?>` and no encoding. `renderliquid="true"` renders the entity's `<records>` through DotLiquid before import; `guidswap="true"` remaps ids consistently. Where the two importers give an attribute different force, the rule above is a warning, never an error, so a TALXIS package validates without errors (see `TestData/CmtPackage/talxis-dialect`).

### Deliberately not validated

Liquid syntax in values (runtime templates are normal data), `displayname` content, schema entities without data, a missing `<m2mrelationships>`, `lookupentityname` content, double-encoded text, and anything that needs Dataverse metadata (entity/column existence, type agreement, option values, lookup targets) or the zip (`files/` presence). Metadata-aware rules are tracked in #124, value parsing per type and file references with them.

### Limits

TXM007's "undeclared name" finding points at the schema file's root (the import order is a list of strings without positions). Hand-edited formatting that `XDocument` cannot represent (attributes split over lines, `/>` without a space) is kept as long as the document is unchanged and normalised on its first real edit.
