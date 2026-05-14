# TALXIS Platform Metadata

Typed C# metadata model, SolutionPackager XML serialization, and workspace validation for Dataverse and Power Platform solution projects.

This repository provides the shared metadata kernel used by TALXIS tooling. It defines what platform components are, how they are loaded from source-controlled solution projects, how they are validated, and how Dataverse-style solution layers are represented in memory. It does not store business data, execute plugins, run workflows, or require the Dataverse SDK.

## What this is for

| Consumer | Usage |
| --- | --- |
| [TALXIS CLI](https://github.com/TALXIS/tools-cli) | Workspace validation, component scaffolding, and language server integration. |
| [TALXIS Build SDK](https://github.com/TALXIS/tools-devkit-build) | Build-time validation, packaging checks, and version stamping. |
| [TALXIS Templates](https://github.com/TALXIS/tools-devkit-templates) | Safe workspace manipulation from template post-actions. |
| Runtime and synchronization services | Future live Dataverse/source-control synchronization and metadata runtime scenarios. |

Use it when you need to inspect, validate, transform, or export unpacked Dataverse solution metadata without taking a dependency on live environment APIs.

## Packages

| Package | Purpose |
| --- | --- |
| `TALXIS.Platform.Metadata` | Core in-memory metadata model for entities, attributes, relationships, forms, views, apps, roles, workflows, solution manifests, component definitions, and solution layers. |
| `TALXIS.Platform.Metadata.Serialization.Xml` | Roundtrip-safe reader/writer for unpacked SolutionPackager XML workspaces, including Power Automate flow JSON and generic component passthrough. |
| `TALXIS.Platform.Metadata.Validation` | Workspace validation: XSD validation, JSON validation, duplicate GUID checks, typed model loading, and load diagnostics with file/line/column locations where available. |

All packages target `netstandard2.0`.

```bash
dotnet add package TALXIS.Platform.Metadata
dotnet add package TALXIS.Platform.Metadata.Serialization.Xml
dotnet add package TALXIS.Platform.Metadata.Validation
```

Add only the packages you need. The core model package has no Dataverse SDK, HTTP, SQL, or runtime service dependency.

## Capabilities

### Metadata model

Typed representations of common Dataverse solution metadata:

- **Entity** - table definitions with ownership, activity type, attributes, relationships, keys, and audit settings.
- **Attribute** - column definitions with types, constraints, option sets, and localized labels.
- **Relationship** - one-to-many, many-to-one, and many-to-many metadata with cascade behavior.
- **Option set** - global and local choices with localized labels.
- **Form** - form XML metadata and mergeable form structure.
- **View** - saved query metadata with FetchXML and layout XML.
- **Security role** - role and privilege metadata.
- **Workflow and flow definition** - classic workflow metadata and Power Automate flow payloads.
- **App, site map, plugin, web resource, and generic components** - typed or passthrough metadata for source-controlled solution contents.

### Solution model

The model separates concepts that Dataverse treats differently:

- **Solutions** - loaded solution manifests.
- **Solution component memberships** - which solution contains or root-owns a component.
- **Source snapshots** - source-owned payloads loaded from individual solution projects for diagnostics and write-back.
- **Layers** - Dataverse-style component layer stacks used for effective-state calculation.
- **Component definitions** - per-type behavior such as identity, merge behavior, overwrite behavior, and dependency metadata.

### Serialization

`TALXIS.Platform.Metadata.Serialization.Xml` reads and writes unpacked SolutionPackager projects. It is designed for source-controlled workspaces where unnecessary diffs are costly:

- Preserves original XML documents and unknown elements where possible.
- Loads single-solution and multi-solution workspaces.
- Tracks source ownership so one solution can be exported from a combined workspace.
- Handles generic components that do not yet have a dedicated typed model.

### Validation

`TALXIS.Platform.Metadata.Validation` validates a workspace as files on disk and as a typed model:

- XML schema validation for embedded SolutionPackager schemas.
- JSON validation for flow definition payloads.
- Duplicate GUID detection.
- Reader/load diagnostics for malformed component files.
- File, line, and column information where available.

## Core concepts

### Workspace

`Workspace` is the in-memory representation of one or more unpacked solution projects. It contains typed component collections such as `Entities`, `Forms`, `Views`, `Workflows`, `FlowDefinitions`, and `GenericComponents`.

It also contains solution-layer state needed for ALM scenarios:

- `Solutions`
- `SolutionComponentMemberships`
- `ComponentSourceSnapshots`
- `Layers`

### Solution layers

The model follows Dataverse ALM terminology:

```text
Active (unmanaged)       maker customizations, one shared layer
Managed solution N       installed in import order
Managed solution 2
Managed solution 1
System                   platform baseline
```

Managed solution projects contribute ordered managed layers. Unmanaged solution projects contribute source-owned snapshots of the shared `Active` layer. Most component types use top-wins resolution; mergeable component types can combine layer payloads.

Layer membership, source ownership, and effective state are intentionally separate. This matters because multiple unmanaged solutions can contain the same component while Dataverse still exposes a single Active layer.

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

Consumers should use the returned file path, line, and column to place editor squiggles or produce build diagnostics at the source of the problem.

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

`XmlWorkspaceWriter.Write(...)` preserves unknown XML and original formatting where possible. If a workspace contains multiple solutions, it throws so callers must choose which solution project to export.

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

1. **Roundtrip safe** - preserve original documents, unknown XML, and source-owned payloads where possible.
2. **Dataverse-aligned** - model solution manifests, memberships, source snapshots, and component layers as distinct concepts.
3. **Diagnostics-friendly** - keep file paths and source locations for validators, language servers, CLI output, and build errors.
4. **Explicit export intent** - multi-solution workspaces require callers to choose the solution they are writing.
5. **Dependency-light** - the core model is pure `netstandard2.0`; XML serialization and validation add only the dependencies they need.

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

See [docs/](docs/) for architecture notes, runtime design, and the roadmap.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
