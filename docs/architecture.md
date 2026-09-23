# Architecture

## Overview

TALXIS Platform Metadata is a typed C# object model for model-driven platform components. It serves as the shared kernel for all tools and services that need to understand, validate, or manipulate platform metadata, whether from files on disk, a live environment API, or an in-memory workspace.

The long-term vision is to build a lightweight, embeddable metadata runtime that replicates Dataverse solution framework behavior (layering, merging, dependency resolution) and can serve metadata via APIs, run in WASM, and power a language server. See [Runtime Architecture](runtime-architecture.md) for the full design.

## Design Philosophy: Simplify, Don't Replicate

Microsoft's SolutionPackager has 35+ dedicated processor classes built up over 15 years. We don't replicate that complexity. Instead:

- **~80% of components are structurally identical** - an XML element extracted from customizations.xml, written to a folder with a naming pattern. The differences are configuration, not behavior.
- **Only ~5 components have special serialization** - Entity (subfolders for forms/views), AppModule (navigation subfolder), PluginAssembly/WebResource (binary + .data.xml), Template (sub-elements as files). Everything else is "extract element → write file."
- **SCF and GenericComponent already prove the simplified model works** - one handler with dynamic config, not a class per type.

So instead of a processor-per-type hierarchy, we use a **data-driven component registry**. Each entry is a `ComponentDefinition` record (`src/TALXIS.Platform.Metadata/ComponentDefinition.cs`):

- identity and layout: type code, name, serialized name, directory, file pattern, identity strategy (GUID, name, composite), aliases
- behavioral flags ported from Dataverse's `IComponentDefinition`: `IsMergeable`, `HasParent`, `RootComponent`, `IsCustomizable`, `CanBeDeleted`, export keys, group parent
- serialization hints: `IsFileBacked` (binary + .data.xml), `HasSubfolders` (Entity, AppModule)

Merge applies to Form/SystemForm, SiteMap, AppModule, AppModuleSiteMap and RibbonCustomization. Everything else, including Entity, is top-wins (see Solution Layering below).

A registry of ~95 definitions replaces the entire processor class hierarchy. The 5 special cases get an `IComponentSerializer` override; everything else uses the default serializer. Today the reader and writer still carry one method per type; the registry-driven default serializer is the target the serialization layer converges to.

## Two Component Architectures

The platform has two distinct component systems (see [blog post](https://blog.networg.com/dataverse-solution-component-types/)):

### Platform Components (type codes 1–660+)
- Fixed, well-known type codes (1=Entity, 2=Attribute, 60=Form, 91=PluginAssembly, etc.)
- Definitions live in `customizations.xml`
- Static XML schemas (XSD-validatable)
- SolutionPackager splits them into per-component files
- Readable diffs in source control

### SCF Components (Solution Component Framework)
- SCF is not a single type code. It is the extensibility framework for dynamically registered components
- Hardcoded types (<1000) have dedicated processors: CustomControl/PCF (66), CanvasApp (300), EnvironmentVariable (380), Bot (403)
- Dynamic types (>=10000) are registered at runtime via `SolutionComponentDefinition` entity (type 270)
- Type codes can differ between environments. Identity is by `ComponentName` + `SchemaName`, not type code
- No static schema; each component owner decides format (JSON or XML)
- Single `ScfPacker` handles all SCF child components for source control integration
- Less readable in source control (GUIDs, encoded properties)
- SmartDiff is built into the framework and applies automatically to all SCF types

Both are first-class in our model. The `ComponentDefinition` registry handles both - platform types are pre-registered with known schemas, SCF types are discovered at runtime.

## Solution Layering

The platform uses a layering system for component state:

```
Active (unmanaged)          ← maker customizations, one shared layer
Managed Solution N          ← installed in order
Managed Solution 2
Managed Solution 1
System                      ← Microsoft out-of-box
```

**Resolution rules:**
- **Most components: top wins** - the highest layer's value is the active state
- **Forms, sitemaps, model-driven apps: merge** - layers are combined, not replaced
- **Managed properties** control what downstream layers can customize

Our model represents layers explicitly. A workspace loads any number of solution projects (managed ones become managed layers, unmanaged ones become source snapshots of the shared Active layer) and answers for every component:

```csharp
var component = workspace.GetComponent(ComponentType.Entity, "udpp_warehouse");
component.Metadata     // the typed EntityMetadata to read or mutate
component.Layers       // [System, ManagedSolution1, Active] in layer order
component.ActiveState  // resolved/merged result, what the environment shows
component.Memberships  // Solution.xml root-component rows that reference it
component.Snapshots    // source projects that own its files (write-back targets)
```

Each typed component knows only itself (`Identity` = type + object id, `DocumentKey` = the source document it owns). Everything about its surroundings comes from the workspace, so components can be created before they belong to any solution.

## Components as Objects

Each component is a C# object, not a raw XML node. The object enforces constraints and tracks state:

```csharp
var workspace = new XmlWorkspaceReader().Load("src/Solutions.DataModel");

var entity = workspace.FindEntity("udpp_warehouse")!;
entity.AddAttribute(new StringAttributeMetadata { LogicalName = "udpp_name", MaxLength = 200 });

new XmlWorkspaceWriter().Write(workspace, "src/Solutions.DataModel"); // only writes changed files, zero diff on untouched files
// multi-solution workspaces write per project: writer.WriteSolution(workspace, "Solutions.DataModel", path)
```

The container stays format-agnostic: loading and saving belong to the serialization packages, never to the container itself. Fluent builders (`EntityBuilder`, `FormBuilder`) will wrap these typed mutations for scaffolding.

### Roundtrip-safe serialization

The model preserves XML elements and attributes it doesn't understand:
- `Load → Save` with no changes = zero git diff
- Unknown children are preserved (forward compatibility)
- Only modified files are written (dirty tracking)

Target implementation: each component has one authoritative persisted document; typed properties are projections over it, unknown nodes pass through untouched. Today the typed classes are plain objects and the serializer keeps the original documents in a per-workspace roundtrip cache that it patches on write. Flow definitions already follow the target (the JSON is authoritative, the typed projection is derived); the XML components converge the same way.

## Workspace Context

The model doesn't touch the filesystem directly. I/O goes through `IWorkspaceContext`:

| Implementation | Use case |
|---|---|
| `FileSystemContext` | Standalone scripts, `dotnet new`, direct disk |
| `TransactionalContext` | CLI - buffered writes, rollback on failure |
| `InMemoryContext` | Language server, tests - no disk |
| `ApiContext` | Live environment metadata (Milestone 6, Provider.Dataverse) |

## Packages and Namespaces

One package per layer; the namespace follows the package except where noted.

```
TALXIS.Platform.Metadata                         package: core, zero dependencies
├── ComponentType (enum), ComponentDefinition, ComponentDefinitionRegistry, IdentityStrategy
├── MetadataBase, Label, metadata contracts (ILocalizedMetadata, IVersionedMetadata, ...)
├── Components/   EntityMetadata, AttributeMetadata + Attributes/* typed subclasses,
│                 RelationshipMetadata, OptionSetMetadata, FormMetadata, SavedQueryMetadata,
│                 SiteMapMetadata, RibbonMetadata, AppModuleMetadata, WebResourceMetadata,
│                 WorkflowMetadata, FlowDefinitionMetadata, PluginAssemblyMetadata, PluginTypeMetadata,
│                 SdkMessageProcessingStepMetadata (+Image), SecurityRoleMetadata, GenericComponentMetadata
├── Solutions/    Solution, Publisher, RootComponent, ComponentIdentity, SolutionComponentMembership,
│                 ComponentSourceSnapshot, ComponentLayer, LayerStack, SolutionLayerManager,
│                 LayerComponentDescriptor, ComponentState, SolutionLayerKind
├── Merging/      MergeableNode, TreeMergeEngine, IComponentMerger + Form/SiteMap/AppModule/Ribbon mergers
├── Controls/     CustomControlMetadata, FormControlBinding
├── Layout/       SolutionPackagerLayout, PathTemplate
└── Schema/       ComponentSchema, ISchemaIntrospector

TALXIS.Platform.Metadata.Workspaces              package: TALXIS.Platform.Metadata.Workspace, deps: core (planned)
├── Workspace (multi-solution container), WorkspaceLoadError
├── IWorkspaceContext, FileSystemContext, TransactionalContext, InMemoryContext
└── WorkspaceBuilder, EntityBuilder, FormBuilder (fluent API for creating components)
    The namespace is plural because the Workspace type cannot share the name of its own namespace.
    Workspace lives in Serialization.Xml until this package exists.

TALXIS.Platform.Metadata.Serialization.Xml       package; deps: core, Workspace, Newtonsoft.Json, System.Text.Json, System.Reflection.MetadataLoadContext
├── XmlWorkspaceReader      - SolutionPackager folder → model (Load, LoadMany)
├── XmlWorkspaceWriter      - model → folder (Write, WriteSolution, roundtrip-safe)
├── IComponentSerializer    - override for the 5 special cases (planned)
└── Scaffolding/            - apply-scaffold appliers (transitional, move onto the typed API)

TALXIS.Platform.Metadata.Validation              package; deps: core, Serialization.Xml
├── WorkspaceValidator, SolutionValidator, SolutionManifestValidator, RelationshipValidator
├── SchemaValidator (XSD), JsonValidator, GuidValidator, Xsd/JsonSchemaIntrospector
└── Schemas/                - embedded XSD resources (36 schemas)

TALXIS.Platform.Metadata.Packaging               package; net10.0; wraps SolutionPackagerLib from the PowerApps CLI
└── SolutionPackagerService - pack/unpack solution ZIPs
```

## Target Framework

`netstandard2.0` for every package except Packaging - maximum compatibility:
- MSBuild tasks (build SDK)
- Template post-action scripts (.NET 10 file-based apps)
- CLI, language server (.NET 10)
- Runtime services (future)

The core package has zero package dependencies (PolySharp is compile-time only) and no System.Xml usage, so it stays AOT and WASM safe. Serialization.Xml adds Newtonsoft.Json, System.Text.Json and System.Reflection.MetadataLoadContext. Packaging targets net10.0 because it hosts the PowerApps CLI packager.
