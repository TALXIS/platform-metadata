# Configuration Migration packages

A Configuration Migration Tool (CMT) package is a `data_schema.xml` that declares tables, columns and how records are matched, and a `data.xml` that holds the records. The library loads a package into a typed model, saves changes back without reformatting the files, and validates it. It does not connect to Dataverse or search for packages: the caller passes the paths.

## Load, change and save

```csharp
var package = new CmtPackageXmlReader().LoadDirectory(@"C:\MyPackage");

var account = package.Schema.FindEntity("account");
account.FindField("telephone1").IsUpdateCompare = true;
account.AddField("websiteurl", CmtFieldTypes.String, "Website");

bool written = new CmtPackageXmlWriter().Save(package, @"C:\MyPackage");
```

- `LoadDirectory` reads `data_schema.xml` and, when present, `data.xml` from a folder. `Load(schemaPath, dataPath)` takes the two files directly.
- Problems found while reading are in `package.LoadErrors`.
- `Save` writes only what changed and keeps everything else in the files as it was. It returns `false` when nothing changed. `SaveSchema` and `SaveData` write one file to a path of your choice.

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
var warnings = new List<string>();
var options = new CmtSchemaBuildOptions { FieldSelection = CmtFieldSelection.Standard, IncludeManyToMany = true };

var account = CmtWorkspaceSchemaBuilder.BuildEntity(workspace, "account", options, warnings, target: package.Schema);
CmtSchemaBuilder.AddOrReplaceEntity(package.Schema, account);
CmtSchemaBuilder.ResolveImportOrder(package.Schema, warnings);
```

- `BuildEntity` maps the table's columns to CMT field types and marks the primary name as `updateCompare`. `CmtSchemaBuilder.BuildEntity` takes the `EntityMetadata` directly.
- Columns CMT cannot migrate, such as bigint and file columns, are left out, and `warnings` says why.
- `AddOrReplaceEntity` keeps fields that are already declared, so hand edits survive a refresh. `MergeEntity` (schema) and `CmtDataBuilder.MergeEntity` (data) combine several packages.
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

The rules follow what Microsoft CMT does on import. Checks that need table metadata, such as whether a column exists in the target environment, are not part of this validation (#124).
