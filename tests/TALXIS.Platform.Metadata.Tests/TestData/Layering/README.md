# Layering regression fixture

`warehouse-item-form.xml` is an unchanged generated Warehouse Item main form from the integration sandbox. It has one tab and nested sections, rows, cells, and controls.

`TreeIdentityRegressionTests.WarehouseForm_UsesTabIdentity` loads this fixture and constructs an upper contribution in memory for the same form. Matching-ID changes must affect the requested tab. Different-ID changes must leave the existing tab intact even when the tab name matches. The fixture is never rewritten.

Run the focused checks from the repository root:

```sh
dotnet test tests/TALXIS.Platform.Metadata.Tests/TALXIS.Platform.Metadata.Tests.csproj --filter FullyQualifiedName~TreeIdentityRegressionTests
```

No external sandbox, credentials, or Dataverse environment is required. These checks validate XML composition, not complete Dataverse import compatibility.
