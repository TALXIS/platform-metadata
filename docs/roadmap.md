# Roadmap

## Phase 1: Foundation - Validation & Schemas
**Goal:** Extract and share what already exists across repos.

- [ ] Create `TALXIS.Platform.Metadata` project (netstandard2.0)
- [ ] Move 23 XSD schemas from [tools-devkit-build](https://github.com/TALXIS/tools-devkit-build/tree/master/src/Dataverse/Tasks/ValidationSchema) as embedded resources
- [ ] Port `ValidateXmlFiles` core logic (XSD validation + nil normalization)
- [ ] Port `ValidateDuplicateGuids` core logic (component identity rules)
- [ ] Port `ComponentType` constants (scattered across build SDK tasks)
- [ ] `SolutionPackagerLayout` - well-known paths (`Other/Solution.xml`, `Entities/`, etc.)
- [ ] Publish to NuGet
- [ ] Update `tools-devkit-build` to consume this package instead of bundling XSDs
- [ ] Wire into CLI's reserved `workspace validate` command

## Phase 2: Serialization - Read SolutionPackager Format
**Goal:** Load a solution workspace from disk into typed objects.

- [ ] `EntityMetadata` - load from `Entity.xml` (name, ownership, attributes, keys)
- [ ] `AttributeMetadata` - typed subclasses (String, Integer, Picklist, Money, Lookup, ...)
- [ ] `RelationshipMetadata` - load from `Relationships/*.xml`
- [ ] `OptionSetMetadata` - global (`OptionSets/*.xml`) and local (inline in Entity.xml)
- [ ] `SolutionMetadata` - load from `Solution.xml` (publisher, version, root components)
- [ ] `CustomizationsMetadata` - load from `Customizations.xml` (component registrations)
- [ ] `SolutionPackagerReader` - load entire workspace into a `Workspace` object
- [ ] Roundtrip test: Load → Save = zero diff (against real solution exports)

## Phase 3: Serialization - Write SolutionPackager Format
**Goal:** Write typed objects back to disk with minimal git diff.

- [ ] `SolutionPackagerWriter` - write `Workspace` to disk
- [ ] `XmlPreservingSerializer` - preserves unknown elements, attribute ordering, whitespace
- [ ] Only write files that have been modified (dirty tracking)
- [ ] Roundtrip test suite: Load from various real-world solutions, Save, verify zero diff

## Phase 4: Workspace Manipulation API
**Goal:** Type-safe, validated, deduplicating component manipulation.

- [ ] `IWorkspaceContext` interface
- [ ] `FileSystemContext` - direct disk (for `dotnet new` scripts)
- [ ] `TransactionalContext` - buffered writes with rollback (for CLI)
- [ ] `SolutionXml.AddRootComponent()` - replaces raw XML manipulation in template scripts
- [ ] `CustomizationsXml.EnsureNode()` - replaces 15+ template scripts that do the same thing
- [ ] `EntityBuilder` - fluent API for creating entities with attributes, forms, views
- [ ] Publish updated package
- [ ] Migrate template post-action scripts to use this API

## Phase 5: Form & View Models
**Goal:** Typed form and view manipulation.

- [ ] `FormMetadata` - tabs, sections, rows, cells, controls with ClassID mapping
- [ ] `ViewMetadata` - FetchXML, layout XML, column definitions
- [ ] `FormBuilder` - fluent API for constructing forms
- [ ] Replace form/view template scripts with typed builders

## Phase 6: Solution Layers
**Goal:** Multi-layer component model mirroring platform behavior.

- [ ] `ComponentLayer` - solution-scoped component state
- [ ] `ComponentDefinition` - per-type behavior (mergeable, dependency rules)
- [ ] `LayerStack` - ordered layers with active resolution
- [ ] Load layers from multiple solution exports
- [ ] Diff between layers (what changed)

## Phase 7: Language Server Integration
**Goal:** Real-time diagnostics and completions in VS Code.

- [ ] `InMemoryContext` - for language server workspace
- [ ] Diagnostics from `SchemaValidator` + `StructuralValidator`
- [ ] Completions from loaded workspace model (entity names, attribute names, option values)
- [ ] Go-to-definition for cross-file references (form → entity → attribute)

## Phase 8: API Loader
**Goal:** Load metadata model from a live environment.

- [ ] `ApiMetadataReader` - `RetrieveEntityRequest` → `EntityMetadata`
- [ ] `ApiMetadataWriter` - `CreateEntityRequest` from model objects
- [ ] Compare disk model vs. live environment (drift detection)

## Existing Code to Consolidate

| Source | What | Destination |
|---|---|---|
| `tools-devkit-build/ValidationSchema/*.xsd` | 23 XSD schemas | `Validation/Schemas/` |
| `tools-devkit-build/Tasks/ValidateXmlFiles.cs` | XSD validation + nil normalization | `Validation/SchemaValidator.cs` |
| `tools-devkit-build/Tasks/ValidateDuplicateGuids.cs` | GUID identity rules per component type | `Validation/GuidValidator.cs` |
| `tools-devkit-build/Tasks/EnsureAllCustomizationsNodes.cs` | Component → Customizations.xml node mapping | `Solutions/ComponentDefinitionRegistry.cs` |
| `tools-cli/DataModelConverter/Model/` | `Table`, `TableRow`, `Relationship`, `OptionsetEnum` | `EntityMetadata`, `AttributeMetadata`, etc. |
| `tools-cli/DataModelConverter/XMLSchemas/OptionSetXmlSchema.cs` | OptionSet XML deserializer | `Serialization/` |
| `client-metadata` (TypeScript) | `IEntityDefinition`, `Attribute`, `DataType` | Port interfaces to C# |
| `INT0014/SDK/ObjectModel/CDS/Solution/` | `Customizations`, `Workflow`, enums | `Solutions/`, `Metadata/` |
