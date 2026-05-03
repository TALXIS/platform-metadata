# Runtime Architecture

## Vision

TALXIS Platform Metadata is the foundation for a lightweight, open-source alternative to Dataverse's metadata runtime. The core library replicates solution framework behavior (layering, merging, dependency resolution) in a format-agnostic, zero-dependency package that runs everywhere: server, CLI, language server, and browser (WASM).

The goal is not to replace Dataverse's data platform, but to provide a fast, embeddable metadata runtime that can:

- Load metadata from any source (unpacked workspace, solution ZIP, live Dataverse, CDN snapshot)
- Resolve the active component state through solution layering (top-wins + merge)
- Serve metadata via APIs optimized for SPA frontends
- Support real-time manipulation (import solution, uninstall, modify components)
- Run in WebAssembly for client-side metadata operations

## Design Constraints

- **Zero dependencies in core** - netstandard2.0, no System.Xml, no Newtonsoft, no EF Core
- **AOT compatible** - no reflection-based serialization, no dynamic code generation
- **WASM compatible** - runs in browser via Blazor/wasm, shared code between server and client
- **Stateless runtime** - load from snapshot, manipulate in memory, serialize back. No database required.
- **Sub-second cold start** - fast snapshot deserialization for language server and SPA scenarios

## Architecture Layers

```
 Providers (load metadata into core model)
 +-----------------------------------------+
 | Serialization.Xml  <- SolutionPackager   |
 | Serialization.Zip  <- solution .zip      |
 | Serialization.Snap <- CDN snapshots      |
 | Provider.Dataverse <- live Dataverse API  |
 +-----------------------------------------+
                    |
                    v load into
 +------------------------------------------+
 |     TALXIS.Platform.Metadata (core)       |
 |     zero deps, AOT/WASM safe              |
 |                                           |
 |  Component Model                          |
 |    EntityMetadata, FormMetadata, etc.      |
 |    MergeableNode (generic tree)            |
 |    Label (multi-language)                  |
 |                                           |
 |  Solution Framework Runtime               |
 |    MetadataRuntime (facade)               |
 |    SolutionLayerManager (layer stacks)    |
 |    IComponentMerger (form/sitemap merge)  |
 |    TreeMergeEngine (format-agnostic diff)  |
 |    DependencyGraph (component refs)       |
 |                                           |
 |  Component Registry                       |
 |    ComponentDefinition (data-driven)      |
 |    IsMergeable, HasParent, RootComponent  |
 |    Runtime-extensible for SCF types       |
 |                                           |
 |  Query API                                |
 |    Find entity/attribute/form by name     |
 |    Resolve effective component from layers |
 |    Enumerate by type, filter by solution  |
 +------------------------------------------+
                    |
                    v read from
 +------------------------------------------+
 |  Output Adapters                          |
 |                                           |
 |  Serialization.Xml  -> SolutionPackager   |
 |  Serialization.Zip  -> solution .zip      |
 |  Serialization.Snap -> CDN snapshots      |
 |  CodeGen.CSharp     -> early-bound C#     |
 |  CodeGen.TypeScript  -> TS types          |
 |  Validation         -> XSD + structural   |
 +------------------------------------------+
                    |
                    v served by
 +------------------------------------------+
 |  Consumers                                |
 |                                           |
 |  REST API (SPA frontend)                  |
 |  Language Server Protocol                 |
 |  CLI commands                             |
 |  Build SDK (MSBuild tasks)               |
 |  Environment Data Service                 |
 +------------------------------------------+
```

## MetadataRuntime (future facade)

The `MetadataRuntime` is the central orchestrator that wires providers, the layer manager, mergers, and the query API into a single entry point:

```
MetadataRuntime
  |
  +-- LoadFromWorkspace(path)     -- unpacked SolutionPackager directory
  +-- LoadFromZip(stream)         -- packed solution ZIP
  +-- LoadFromSnapshot(stream)    -- CDN-optimized binary format
  +-- LoadFromDataverse(client)   -- live environment via SDK
  |
  +-- ImportSolution(stream)      -- add layers, recompute active state
  +-- UninstallSolution(name)     -- remove layers, cascade, recompute
  |
  +-- GetEffective<T>(type, id)   -- resolved component after layering
  +-- Query(filter)               -- enumerate components with predicates
  |
  +-- OnChanged                   -- event for API/LSP/subscriber notification
  |
  +-- SerializeSnapshot(stream)   -- export current state for CDN/cache
```

### Import flow

```
Solution ZIP arrives
  -> Provider deserializes into List<MetadataBase> objects
  -> SolutionLayerManager.ImportSolutionLayer() adds new layer
  -> For each affected component:
     - Non-mergeable: top-wins (active layer replaces previous)
     - Mergeable (forms, sitemaps): IComponentMerger combines all layers
  -> DependencyGraph validates no broken references
  -> OnChanged fires for all modified components
  -> Consumers (API, LSP) get notified of changes
```

### Uninstall flow

```
UninstallSolution("SolutionName")
  -> DependencyGraph checks no other solution depends on these components
  -> SolutionLayerManager.RemoveSolutionLayers("SolutionName")
  -> For each affected component:
     - Recompute active state from remaining layers
     - Components with no remaining layers are deleted
  -> Schema changes propagated (if backed by SQL)
  -> OnChanged fires
```

## Multi-Source Loading

All providers load into the same in-memory model. The runtime doesn't care where metadata came from.

### Source: Unpacked workspace (Serialization.Xml)

The current primary source. SolutionPackager format on disk. Used by:
- CLI (`workspace validate`, `workspace explain`)
- Build SDK (MSBuild validation tasks)
- Language server (file watching + incremental reload)
- Template engine (post-action scripts)

### Source: Solution ZIP (Serialization.Zip, future)

Packed solution files as produced by `pac solution pack` or downloaded from Dataverse. The ZIP contains `solution.xml` + `customizations.xml` + individual component files.

Flow: unzip to temp -> delegate to Serialization.Xml -> load into model.

More important use: `MetadataRuntime.ImportSolution(stream)` accepts a ZIP and adds it as a new solution layer, recomputing the active state. This replicates Dataverse's `ImportSolutionRequest`.

### Source: Live Dataverse (Provider.Dataverse, future)

Uses `RetrieveMetadataChangesRequest` for entities/attributes/relationships and `RetrieveMultiple` for forms, views, plugins, etc. Supports incremental sync via server version tokens.

Used by:
- CLI environment commands
- Drift detection (compare workspace vs live)
- Initial metadata download for offline development

### Source: CDN snapshot (Serialization.Snap, future)

A purpose-built binary format optimized for fast deserialization and CDN serving. See "Snapshot Format" section below.

## Snapshot Format (future)

The XML format is too verbose and slow to parse for SPA/WASM scenarios. We need a compact, CDN-friendly snapshot format.

### Requirements

- **Fast deserialization** - sub-100ms for a typical solution (~200 components)
- **Content-addressable** - file paths derived from content hash for CDN cache busting
- **Filterable by path** - clients can request specific subsets without downloading everything
- **Streamable** - can start rendering before full download completes
- **WASM-friendly** - no XML parser needed, minimal allocations

### Proposed approach: content-addressable component store

```
snapshot/
  manifest.json              -- version, publisher, component index
  components/
    {content-hash}.bin       -- each component serialized independently
  indices/
    by-type/{typeCode}.json  -- component IDs by type (for filtered loading)
    by-entity/{name}.json    -- all components for an entity (forms, views, attrs)
    by-app/{name}.json       -- all components for an app module
```

**Manifest** lists all components with their content hashes:
```json
{
  "version": "1.0.0.0",
  "publisher": "talxis",
  "components": [
    { "type": 1, "id": "account", "hash": "a1b2c3d4", "size": 4096 },
    { "type": 60, "id": "form-guid", "hash": "e5f6g7h8", "size": 2048 }
  ]
}
```

**CDN URL pattern**: `https://cdn.example.com/metadata/{env-hash}/components/{content-hash}.bin`

**Client workflow**:
1. Fetch `manifest.json` (small, always fresh or versioned)
2. Fetch only the indices needed (e.g., `by-app/my-app.json`)
3. Fetch only the components listed in the index
4. Components are immutable (content-addressed) so CDN caches them indefinitely

**Serialization format for .bin files**: MessagePack or a custom binary format. Must be:
- Deserializable in WASM (no System.Xml dependency)
- Compact (smaller than JSON)
- Schema-evolvable (field tags, not position-based)

### Inspiration from INT0014-MetadataService

The existing MetadataService uses version-tagged URLs for long-term caching (`?metadataVersion=v1_xxxxx` -> 30-day max-age). This pattern works well and should be adopted. Their preload orchestrator (discover all entities/forms/views for an app module, then fetch all in parallel) is the right UX pattern.

Key differences from our approach:
- They cache processed JSON in Dataverse file columns (we'll use CDN-native files)
- They proxy live Dataverse on every request (we'll serve from computed snapshots)
- They have no solution layering (we'll have full layer resolution)

## Solution Framework Behavior Reference

Based on decompiled Dataverse code (source of truth).

### Component Resolution

Most components use **top-wins**: the topmost active layer's version is the effective component. Only a few types use **merge**:

| Component | Resolution | Merge strategy |
|-----------|-----------|----------------|
| Entity, Attribute, Relationship, OptionSet | Top-wins | N/A |
| PluginAssembly, SdkMessageProcessingStep | Top-wins | N/A |
| SecurityRole, WebResource, Workflow | Top-wins | N/A |
| **Form (SystemForm)** | **Merge** | solutionaction-based XML diff/merge |
| **SiteMap** | **Merge** | solutionaction-based XML diff/merge |
| **AppModule** | **Merge** | solutionaction-based XML diff/merge |

### Layer Ordering

From bottom to top:
1. **System layer** - OOB Microsoft definitions
2. **Managed layers** - installed managed solutions, ordered by install sequence
3. **Active (unmanaged) layer** - all unmanaged customizations share one layer

Managed layer order is determined by `LayerSequenceIndex` assigned at import time.

### Base Language Rules

- Base language label is never deleted on import (even with mergeLabels=false)
- Auto-created from first available label if missing
- Non-provisioned languages are silently dropped
- All languages live inline in XML AND in RESX when `--localize` is used

### Label XML Patterns

Three distinct patterns across component types:

| Pattern | Elements | Used by |
|---------|----------|---------|
| PascalCase | `<LocalizedNames>/<LocalizedName>` | Entity, Solution, Publisher, AppModule |
| lowercase | `<labels>/<label>` | Form content (tab/section/cell), OptionSet options |
| lowercase | `<displaynames>/<displayname>` | Attributes, OptionSet display names |
| Unique | `<Titles>/<Title LCID="" Title="">` | SiteMap groups (different attribute names) |

### SCF Components

SCF (Solution Component Framework) is the extensibility mechanism for dynamically registered solution-aware entities. NOT a single component type:

- **Hardcoded types (<1000)**: Entity (1), CustomControl/PCF (66), CanvasApp (300), EnvironmentVariable (380), Bot (403) each have dedicated processors
- **Dynamic types (>=10000)**: Registered at runtime via `SolutionComponentDefinition` entity (type 270)
- Identity via `ComponentName` + `SchemaName`, not type code (codes can differ between environments)
- Single `ScfPacker` handles all SCF child components for source control integration

Our `ComponentDefinitionRegistry` can be extended at runtime when connected to a live environment by loading `SolutionComponentDefinition` records.

## Lessons from INT0014-EnvironmentDataService

The team built a functional Dataverse data platform (OData+FetchXml CRUD) but only scratched the surface of solution framework behavior.

### What to borrow

- **EDMX-first metadata -> SQL DDL pipeline**: solution XML -> EDMX -> diff -> SQL migration is sound for dynamic schema management. When we need to back our runtime with a database, this pattern (generate DDL from metadata diff) is the right approach.
- **OData query processing**: their request parser -> query builder -> SQL generator -> materializer pipeline is well-structured.
- **Provider abstraction**: `IManifestProvider`, `ISqlDataProvider`, `IFileStorageService` enable swappable backends.
- **Layer sequence index**: simple integer ordering for solution layers.

### What to avoid

- **Reflection-based merging**: their `ApplyAttributeChange` copies non-null properties via reflection. This is type-unsafe, hard to debug, and doesn't match Dataverse's explicit per-type merge strategies.
- **Full recalculation on every import**: they re-parse ALL solutions from disk on every import. Our `SolutionLayerManager` should incrementally update only affected components.
- **Ignoring solutionaction**: they model the attribute but never process it. Form merge is the most complex and important behavior.
- **No managed/unmanaged distinction**: all solutions treated identically. Managed properties control downstream customization and must be enforced.
- **NOT NULL -> NULL hack**: blanket removal of nullability constraints is a data integrity problem.
- **Filesystem-only metadata**: no queryable metadata store. We need the in-memory model to be queryable.
- **Monolithic merge**: one `Apply()` method handles 4 types. We use `IComponentMerger` with per-type strategies dispatched by `SolutionLayerManager.Resolve()`.

### Their Customization.Model.cs (~6270 lines)

Comprehensive XML deserialization model for `customizations.xml`. Covers entities, attributes, forms (Tab/Section/Row/Cell/Control), views, sitemaps, app modules, ribbons, dashboards, web resources. Could serve as a reference for XML structure, but we should not adopt the approach. Our core model is format-agnostic, and XML mapping lives in Serialization.Xml.

## Package Catalog (current + planned)

| Package | Status | Deps | Purpose |
|---------|--------|------|---------|
| `TALXIS.Platform.Metadata` | Published | none | Core model, merging, layering, registry |
| `.Serialization.Xml` | Published | core | SolutionPackager XML read/write |
| `.Validation` | Published | core | XSD schemas + structural validators |
| `.Serialization.Zip` | Planned | core + Xml | Solution ZIP pack/unpack |
| `.Serialization.Snap` | Planned | core | CDN-optimized snapshot format |
| `.Provider.Dataverse` | Planned | core | Load/push metadata via Dataverse SDK |
| `.CodeGen.CSharp` | Planned | core | Generate early-bound C# entity classes |
| `.CodeGen.TypeScript` | Planned | core | Generate TypeScript type definitions |
