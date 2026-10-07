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

var data = new CmtData();
data.AddEntity("account")
    .AddRecord(new Guid("5f1c2a40-7b3e-4c6d-9e8f-0a1b2c3d4e5f"))
    .Set("name", "Contoso");

new CmtPackageXmlWriter().Save(new CmtPackage(schema, data), @"C:\MyPackage");
```

Use fixed record ids, not `Guid.NewGuid()`, so that re-importing a package updates the same records instead of creating new ones.

## Validate

```csharp
var findings = new List<ValidationResult>();
findings.AddRange(new CmtDataSchemaValidator().Validate(package.Schema));
findings.AddRange(new CmtPackageValidator().Validate(package));
```

- `CmtDataSchemaValidator` checks the schema on its own; `CmtPackageValidator` checks `data.xml` against it.
- `WorkspaceValidator.ValidateDirectory` runs both for every package it finds under a workspace folder, together with the XSD check.
- Each finding carries a stable `Code`. The codes and what they mean are defined in `ValidationDiagnostics`.

The rules follow what Microsoft CMT does on import. Packages written for the TALXIS importer, which adds attributes such as `renderliquid` and `guidswap`, validate without errors. Checks that need table metadata, such as whether a column exists in the target environment, are not part of this validation (#124).
