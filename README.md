# TALXIS Platform Metadata

Typed metadata model, SolutionPackager XML serialization, and workspace validation for Dataverse and Power Platform solution projects.

This library is the shared metadata kernel used by TALXIS tooling. It models solution components, validates unpacked solution workspaces, preserves source ownership, and calculates Dataverse-style solution layers without depending on the Dataverse SDK.

## Packages

| Package | Purpose |
| --- | --- |
| `TALXIS.Platform.Metadata` | Core in-memory metadata model for entities, attributes, relationships, forms, views, apps, roles, workflows, solution manifests, component definitions, and solution layers. |
| `TALXIS.Platform.Metadata.Serialization.Xml` | Roundtrip-safe reader/writer for unpacked SolutionPackager XML workspaces, including Power Automate flow JSON and generic component passthrough. |
| `TALXIS.Platform.Metadata.Validation` | Unified workspace validation: XSD validation, JSON validation, duplicate GUID checks, model loading, and load diagnostics with file/line/column locations where available. |

All packages target `netstandard2.0`.

## Install

```bash
dotnet add package TALXIS.Platform.Metadata
dotnet add package TALXIS.Platform.Metadata.Serialization.Xml
dotnet add package TALXIS.Platform.Metadata.Validation
```

Add only the packages you need. The core model package has no Dataverse SDK, HTTP, SQL, or runtime service dependency.

## What it is for

- Build tools that need to inspect or transform unpacked Dataverse solution projects.
- Language servers and editors that need typed model loading plus diagnostics with source locations.
- CLI validation and scaffolding workflows.
- Release pipelines that need deterministic solution metadata checks before packaging/import.
- Future Dataverse/source-control synchronization where one in-memory workspace contains multiple managed and unmanaged solutions.

## Core concepts

### Workspace

`Workspace` is the in-memory representation of one or more unpacked solution projects. It contains typed component collections such as `Entities`, `Forms`, `Views`, `Workflows`, `FlowDefinitions`, and `GenericComponents`.

It also tracks release-critical solution state separately:

- `Solutions` - loaded solution manifests.
- `SolutionComponentMemberships` - which solution contains or root-owns a component, mirroring Dataverse `solutioncomponent` membership.
- `ComponentSourceSnapshots` - source-owned payloads loaded from solution projects for diagnostics and write-back.
- `Layers` - Dataverse-style component layer stacks used for effective-state calculation.

### Solution layers

The model follows Dataverse ALM terminology:

- Managed solution projects contribute ordered managed layers.
- Unmanaged solution projects contribute source-owned snapshots of the shared `Active` layer.
- The Active/unmanaged layer sits above managed layers.
- Most component types use top-wins resolution.
- Model-driven apps, forms, and site maps are mergeable component types in Dataverse; the library also includes a merge strategy for ribbon customization payloads.

Layer membership, source ownership, and effective state are intentionally separate. This is important because multiple unmanaged solutions can contain the same component while Dataverse still exposes a single Active layer.

## Validate a workspace

```csharp
using TALXIS.Platform.Metadata.Validation;

var validator = new WorkspaceValidator();
var report = validator.ValidateDirectory("src/Solutions/Core");

foreach (var result in report.Results)
{
    Console.WriteLine($"{result.Severity}: {result.FilePath}({result.Line},{result.Column}) {result.Message}");
}

if (report.LoadedComponents != null)
{
    Console.WriteLine(report.LoadedComponents);
}
```

Validation includes XML schema checks, JSON checks for flow definitions, duplicate GUID detection, typed model loading, and load diagnostics. Consumers should use the returned file path, line, and column to place editor squiggles where the problem was found.

## Load and write one solution project

```csharp
using TALXIS.Platform.Metadata.Serialization.Xml;

var reader = new XmlWorkspaceReader();
var workspace = reader.Load("src/Solutions/Core");

Console.WriteLine($"Solutions: {workspace.Solutions.Count}");
Console.WriteLine($"Entities: {workspace.Entities.Count}");

var writer = new XmlWorkspaceWriter();
writer.Write(workspace, "artifacts/Core");
```

`XmlWorkspaceWriter` preserves unknown XML and original formatting where possible. If a workspace contains multiple solutions, `Write(...)` throws so callers must choose which solution project to export.

## Load multiple solutions into one workspace

```csharp
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Serialization.Xml;

var reader = new XmlWorkspaceReader();

var workspace = reader.LoadMany(new[]
{
    new SolutionWorkspaceSource("src/Solutions/BaseManaged", importOrder: 0),
    new SolutionWorkspaceSource("src/Solutions/AppManaged", importOrder: 10),
    new SolutionWorkspaceSource("src/Solutions/UnmanagedCustomizations", importOrder: 100)
});

var accountStack = workspace.Layers.FindStack(ComponentType.Entity, "account");
var effectiveAccount = accountStack == null ? null : workspace.Layers.Resolve(accountStack);

Console.WriteLine($"Loaded solutions: {workspace.Solutions.Count}");
Console.WriteLine($"Component memberships: {workspace.SolutionComponentMemberships.Count}");
Console.WriteLine($"Source snapshots: {workspace.ComponentSourceSnapshots.Count}");
Console.WriteLine($"Layer stacks: {workspace.Layers.Stacks.Count}");
```

`LoadMany(...)` requires each `SolutionWorkspaceSource` to contain exactly one solution manifest. `ImportOrder` is caller-defined so Package Deployer order, manual import order, or test fixtures can be represented explicitly.

## Export one solution from a multi-solution workspace

```csharp
using TALXIS.Platform.Metadata.Serialization.Xml;

var writer = new XmlWorkspaceWriter();
writer.WriteSolution(workspace, "UnmanagedCustomizations", "artifacts/UnmanagedCustomizations");
```

`WriteSolution(...)` exports the selected solution project using source ownership metadata. It does not blindly write the currently effective Active snapshot into every solution.

## Work with layers directly

```csharp
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

var layers = new SolutionLayerManager();

var managed = new Solution { UniqueName = "Base", IsManaged = true };
layers.ImportManagedLayer(
    managed,
    importOrder: 0,
    new[]
    {
        new LayerComponentDescriptor(
            ComponentType.Entity,
            "account",
            new EntityMetadata { LogicalName = "account" })
    });

var active = new Solution { UniqueName = "LocalCustomizations", IsManaged = false };
layers.ImportActiveLayerSnapshot(
    active,
    importOrder: 100,
    new[]
    {
        new LayerComponentDescriptor(
            ComponentType.Entity,
            "account",
            new EntityMetadata { LogicalName = "account", IsAuditEnabled = true })
    });

var stack = layers.FindStack(ComponentType.Entity, "account");
var effective = stack == null ? null : layers.Resolve(stack);
```

Use `ImportManagedLayer(...)` for managed solution imports and `ImportActiveLayerSnapshot(...)` for unmanaged source projects. Avoid treating every unmanaged solution as a separate runtime layer; unmanaged projects share the Dataverse Active layer.

## Design principles

1. **Roundtrip safe** - preserve unknown XML and source documents where possible.
2. **Dataverse-aligned** - model solution manifests, memberships, source snapshots, and component layers as distinct concepts.
3. **Diagnostics-friendly** - keep file paths and source locations for validators, language servers, and CLI output.
4. **Explicit export intent** - multi-solution workspaces require `WriteSolution(...)`.
5. **Dependency-light** - the core model is pure `netstandard2.0`; XML and validation packages add only the dependencies they need.

## Current scope

Implemented:

- Typed metadata model for common Dataverse solution components.
- Solution manifest, publisher, root component, component definition, and layer models.
- Single- and multi-solution workspace loading.
- Source-owned snapshots and solution/component membership tracking.
- Explicit single-solution export from multi-solution workspaces.
- XSD, JSON, duplicate GUID, and model-load validation.

Tracked follow-up work:

- Complete dependency graph and uninstall-safety simulation.
- Package Deployer import-order discovery.
- Patch, holding, and staged upgrade semantics.
- Managed property and publisher customizability enforcement.
- Complete solution component type parity.
- Live Dataverse provider/source-control synchronization.

## Related projects

| Consumer | Usage |
| --- | --- |
| [TALXIS CLI](https://github.com/TALXIS/tools-cli) | Workspace validation, component scaffolding, language server integration. |
| [TALXIS Build SDK](https://github.com/TALXIS/tools-devkit-build) | Build-time validation, packaging, and version stamping. |
| [TALXIS Templates](https://github.com/TALXIS/tools-devkit-templates) | Safe workspace manipulation from template post-actions. |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
