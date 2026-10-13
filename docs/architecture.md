# Architecture

## Overview

TALXIS Platform Metadata is a typed C# object model for model-driven platform components. It serves as the shared kernel for all tools and services that need to understand, validate, or manipulate platform metadata, whether from files on disk, a live environment API, or an in-memory workspace.

The long-term vision is to build a lightweight, embeddable metadata runtime that replicates Dataverse solution framework behavior (layering, merging, dependency resolution) and can serve metadata via APIs, run in WASM, and power a language server. See [Runtime Architecture](runtime-architecture.md) for the full design.

## Design Philosophy: Simplify, Don't Replicate

Microsoft's SolutionPackager has 35+ dedicated processor classes built up over 15 years. We don't replicate that complexity. Instead:

- **~80% of components are structurally identical** - an XML element extracted from customizations.xml, written to a folder with a naming pattern. The differences are configuration, not behavior.
- **Only ~5 components have special serialization** - Entity (subfolders for forms/views), AppModule (navigation subfolder), PluginAssembly/WebResource (binary + .data.xml), Template (sub-elements as files). Everything else is "extract element → write file."
- **SCF and GenericComponent already prove the simplified model works** - one handler with dynamic config, not a class per type.

So instead of a processor-per-type hierarchy, we use a **data-driven component registry**:

```csharp
record ComponentDefinition(
    int TypeCode,
    string Name,
    string XmlElementName,       // "Entities", "Roles", "Workflows", "SCF"
    string Directory,            // "Entities", "Roles", "Workflows"
    string FilePattern,          // "$(PrimaryName)/Entity.xml", "$(PrimaryName).xml"
    IdentityStrategy Identity,   // GUID, Name, or Composite
    bool SupportsMerge = false,  // only Entity, SiteMap, AppModule, AppModuleSiteMap
    bool IsFileBacked = false,   // binary + .data.xml (WebResource, Plugin, Report)
    bool HasSubfolders = false   // Entity (forms/views/visualizations), AppModule
);
```

A registry of ~95 definitions replaces the entire processor class hierarchy. The 5 special cases get an `IComponentSerializer` override. Everything else uses the default serializer.

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

### Schema validation of solution component instances

Solution component instances (records of solution-aware tables, enabled per table by a row in `Assets/solutioncomponentconfigurations.xml` and an `IsExportKey` key, and SCF components) unpack to one file per instance. The root element is named after the table and the children are its columns, so the instance's schema is the table definition in `Entity.xml`, which an embedded XSD cannot express:

```
<solution root>/<folder>/<key>/<name>.xml       e.g. tst_widgets/tst_default/tst_widget.xml
<tst_widget tst_unique_name="tst_default"> ... column values ... </tst_widget>
```

`SolutionValidator` skips XSD validation of such a file without a finding when no embedded XSD declares its root element, the root element equals `<name>`, and one of its attributes has the value `<key>`. Any other XML file whose root no XSD declares gets a single `TXM027` warning instead of a schema error, so a component type Microsoft introduces does not fail validation; consumers can raise or suppress `TXM027` by code. `solutioncomponentconfigurations.xml` itself is validated by `SolutionComponentConfiguration.xsd`.

Not covered yet:
- **Instance contents.** Instances are not checked against their table definition (columns exist, root attribute is the export key). The definition often ships in a different solution than the instances, and the instance format is undocumented.
- **JSON instances.** Tables configured with `fileformat` 1 export JSON instances. `JsonValidator` leaves JSON files that match no embedded JSON schema untouched, so these instances are neither validated nor reported.
- **Single-file record layout.** With `filescope` other than Individual, records of a table are exported as one `Assets/<entitysetname>.xml` file (root element is the entity set name, one child per record keyed by primary id). These files get `TXM027`.
- **N:N associations between records.** Exports place intersect rows as `<{relationship}set>` elements in `Other/Customizations.xml`, which `Solution.xsd` does not allow.
- **`solutioncomponentattributeconfigurations` and `solutioncomponentrelationshipconfiguration`.** No export sample shows their shape, so they have no schema and get `TXM027`.

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

Our model represents layers explicitly:

```csharp
var component = workspace.GetComponent(ComponentType.Entity, "udpp_warehouse");
component.Layers       // [System, ManagedSolution1, Active]
component.ActiveState  // resolved/merged result
```

## Components as Objects

Each component is a C# object, not a raw XML node. The object enforces constraints and tracks state:

```csharp
var workspace = Workspace.Load("src/Solutions.DataModel");

var entity = workspace.Entities["udpp_warehouse"];
entity.AddAttribute(new StringAttribute("udpp_name") { MaxLength = 200 });

workspace.Save(); // only writes changed files, zero diff on untouched files
```

### Roundtrip-safe serialization

The model preserves XML elements and attributes it doesn't understand:
- `Load → Save` with no changes = zero git diff
- Unknown children are preserved (forward compatibility)
- Only modified files are written (dirty tracking)

Implementation: model classes wrap the original `XElement`. Known properties read/write through it. Unknown nodes pass through untouched.

## Workspace Context

The model doesn't touch the filesystem directly. I/O goes through `IWorkspaceContext`:

| Implementation | Use case |
|---|---|
| `FileSystemContext` | Standalone scripts, `dotnet new`, direct disk |
| `TransactionalContext` | CLI - buffered writes, rollback on failure |
| `InMemoryContext` | Language server, tests - no disk |
| `ApiContext` | Live environment metadata (future) |

## Namespace Structure

```
TALXIS.Platform.Metadata
├── ComponentType (enum - all ~95 type codes)
├── ComponentDefinition, ComponentDefinitionRegistry
├── IdentityStrategy (enum - GUID, Name, Composite)
├── Label, LocalizedLabel
└── enums: OwnershipType, AttributeType, RelationshipType, ...

TALXIS.Platform.Metadata.Components
├── EntityMetadata, AttributeMetadata (typed subclasses)
├── RelationshipMetadata, OptionSetMetadata
├── FormMetadata, ViewMetadata
├── PluginAssemblyMetadata, SecurityRoleMetadata
└── ScfComponentMetadata (generic for SCF types)

TALXIS.Platform.Metadata.Solutions
├── Solution, Publisher
├── SolutionComponent, ComponentLayer
├── LayerStack (resolution logic)
└── ComponentState, ComponentOperation (enums)

TALXIS.Platform.Metadata.Serialization
├── SolutionPackagerReader  - disk → model
├── SolutionPackagerWriter  - model → disk (roundtrip-safe)
└── IComponentSerializer    - override for the 5 special cases

TALXIS.Platform.Metadata.Validation
├── SchemaValidator         - XSD-based
├── StructuralValidator     - cross-file consistency
└── Schemas/                - embedded XSD resources (23 schemas)

TALXIS.Platform.Metadata.Workspace
├── IWorkspaceContext
├── FileSystemContext, TransactionalContext, InMemoryContext
└── WorkspaceBuilder (fluent API for creating components)
```

## Target Framework

`netstandard2.0` - maximum compatibility:
- MSBuild tasks (build SDK)
- Template post-action scripts (.NET 10 file-based apps)
- CLI, language server (.NET 10)
- Runtime services (future)

Zero external dependencies beyond `System.Xml.Linq` and `System.Text.Json`.
