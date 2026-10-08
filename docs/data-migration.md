# Configuration Migration packages

`TALXIS.Platform.Metadata.DataMigration` models the file format of Microsoft's [Configuration Migration tool (CMT)](https://learn.microsoft.com/power-platform/admin/manage-configuration-data), the `data_schema.xml` and `data.xml` pair that `pac data` and Package Deployer import and export. Reference that package (namespace `TALXIS.Platform.Metadata.DataMigration`) for the model, reader, writer and schema builders, and `TALXIS.Platform.Metadata.Validation` for the checks. The runtime does not load it.

The `data_schema.xml` declares tables, columns and how records are matched; the `data.xml` holds the records. The library loads a package into a typed model, saves changes back without reformatting the files, and validates it. It does not connect to Dataverse or search for packages: the caller passes the paths.

## Load, change and save

```csharp
using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Validation;

var package = new CmtPackageXmlReader().LoadDirectory(@"C:\MyPackage");

var account = package.Schema.FindEntity("account");
account.FindField("telephone1").IsUpdateCompare = true;
account.AddField("websiteurl", CmtFieldTypes.String, "Website");

bool written = new CmtPackageXmlWriter().Save(package, @"C:\MyPackage");
```

- `LoadDirectory` reads `data_schema.xml` and, when present, `data.xml` from a folder. `Load(schemaPath, dataPath)` takes the two files directly.
- Problems found while reading are in `package.LoadErrors`.
- `Save` writes only what changed and keeps everything else in the files as it was. It returns `false` when nothing changed. `SaveSchema` and `SaveData` write one file to a path of your choice.
- Attributes the model has no property for, such as the TALXIS importer's `guidswap`, are kept in `OtherAttributes` on each element and written back, also when a package is merged or written from scratch.

## Create a package

```csharp
var schema = new CmtDataSchema();
schema.AddEntity("account", "accountid", "name", "Account")
      .AddField("name", CmtFieldTypes.String, "Account Name", updateCompare: true);

var id = new Guid("5f1c2a40-7b3e-4c6d-9e8f-0a1b2c3d4e5f");
var data = new CmtData();
data.AddEntity("account")
    .AddRecord(id)
    .Set("accountid", id.ToString())
    .Set("name", "Contoso");

new CmtPackageXmlWriter().Save(new CmtPackage(schema, data), @"C:\MyPackage");
```

Use fixed record ids, not `Guid.NewGuid()`, and set the id both on the record and in its primary-key field: CMT takes the record id from that field and creates a new id without it. Re-importing the package then updates the same records instead of creating new ones.

## Build schema entries from metadata

```csharp
var tables = new[] { "account", "contact" };
var options = new CmtSchemaBuildOptions { FieldSelection = CmtFieldSelection.Standard, IncludeManyToMany = true };

// First pass: declare every table, so relationship entries can point at any of them.
foreach (var table in tables)
    CmtSchemaBuilder.AddOrReplaceEntity(package.Schema, CmtWorkspaceSchemaBuilder.BuildEntity(workspace, table, options, new List<string>(), target: package.Schema));

// Second pass: the refresh takes the relationships of the rebuilt entity, now built against every table.
var warnings = new List<string>();
foreach (var table in tables)
    CmtSchemaBuilder.AddOrReplaceEntity(package.Schema, CmtWorkspaceSchemaBuilder.BuildEntity(workspace, table, options, warnings, target: package.Schema));
CmtSchemaBuilder.ResolveImportOrder(package.Schema, warnings);
```

- `BuildEntity` maps the table's columns to CMT field types and marks the primary name as `updateCompare`. `CmtSchemaBuilder.BuildEntity` takes the `EntityMetadata` directly.
- Relationship entries depend on what `target` declares when `BuildEntity` runs: an N:1 entry is written only when the referenced table is already in the schema, and a many-to-many to a table it does not declare is reported. Build every table once, then build each again as above; tables the package already declares need only the second pass.
- Columns CMT cannot migrate, such as bigint, are left out, and `warnings` says why. File columns are declared as `filedata`; exporting them needs file export to be on (`txc data package export --export-files`).
- `AddOrReplaceEntity` keeps fields that are already declared, so hand edits survive a refresh.
- `MergeEntity` (schema) and `CmtDataBuilder.MergeEntity` (data) combine several packages. The first package wins, and `warnings` lists every difference the import would notice: entity attributes, field and relationship declarations, field values (including lookup names, file names and attendees), `newId`, and associations that name another target table.
- `ResolveImportOrder` puts every table after the tables it looks up. Pass `manualOrder` to keep a hand-written order.

## Validate

```csharp
var findings = new List<ValidationResult>();
findings.AddRange(new CmtDataSchemaValidator().Validate(package.Schema));
findings.AddRange(new CmtPackageValidator().Validate(package));
```

- `CmtDataSchemaValidator` checks the schema on its own; `CmtPackageValidator` checks `data.xml` against it.
- `WorkspaceValidator.ValidateDirectory` runs both for every package it finds under a workspace folder, together with the XSD check.
- Each finding carries a stable `Code`. The codes and what they mean are defined in `ValidationDiagnostics`.

The rules follow what Microsoft CMT does on import.

## Validate against metadata

```csharp
var metadata = new XmlWorkspaceReader().LoadMany(solutionSources);
var options = new CmtMetadataValidationOptions { IncompleteMetadataSeverity = ValidationSeverity.Warning };
var findings = new CmtMetadataValidator().Validate(package, metadata, options);
```

- `CmtMetadataValidator` checks a package against the tables it will be imported into. The metadata is a `Workspace`: loaded from solutions offline, or filled from a live environment by the caller.
- Solutions often hold only part of what the package uses: tables from other solutions or the platform, and tables added without all their columns (recognised by a missing primary id column). Findings that may only mean "the metadata does not cover this" take `IncompleteMetadataSeverity`. Keep it at `Error` against a live environment and lower it to `Warning` against solutions.
- The system tables every export points to (`systemuser`, `team`, `businessunit`, `transactioncurrency`, `organization`) are not reported as missing lookup targets.
- Tables with `renderliquid="true"` keep their values unchecked, since they may be Liquid templates until the TALXIS importer renders them.

| Rule | Severity | What it reports |
|---|---|---|
| `CmtEntityNotInMetadata` | incomplete metadata; `Error` when the table does not accept imports | a schema table that is not in the metadata, or one CMT hides because it is not importable |
| `CmtFieldNotInMetadata` | `Error`; incomplete metadata on a partly held table | a schema field that is not a column of its table |
| `CmtFieldTypeMismatch` | `Error` | a field type that is not the type CMT uses for the column (`lookup`, `customer` and `file` are accepted as the importers read them) |
| `CmtManyToManyNotInMetadata` | `Error`; incomplete metadata on a partly held table | a many-to-many entry that is not a relationship whose first table (`FirstEntityName`) is its table |
| `CmtPrimaryFieldMismatch` | `Warning` | `primaryidfield` or `primarynamefield` other than the table's primary columns |
| `CmtLookupTargetNotAllowed` | `Warning` | a `lookupType` table the lookup column cannot point to |
| `CmtRelationshipNotInMetadata` | `Warning` | an N:1 entry the metadata does not have, or with other columns or tables |
| `CmtFieldNotWritable` | `Warning` | a column valid neither for create nor for update, whose values are dropped |
| `CmtDataOptionValueUnknown` | `Warning` | a choice value in `data.xml` that is not an option of the column |
| `CmtDataLookupEntityUnknown` | incomplete metadata | a `data.xml` lookup to a table that is not in the metadata |
