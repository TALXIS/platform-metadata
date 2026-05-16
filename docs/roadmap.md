# Roadmap

## Milestone 1: Foundation (DONE)

Three packages published to NuGet (v0.1.3):
- `TALXIS.Platform.Metadata` - core model (zero deps, netstandard2.0)
- `TALXIS.Platform.Metadata.Serialization.Xml` - SolutionPackager XML read/write
- `TALXIS.Platform.Metadata.Validation` - 21 XSD schemas + GUID/JSON validators

Wired into consumers: Build SDK references Validation, CLI has `workspace validate`.

## Milestone 2: Expanded Model + Layering (DONE)

- 12 new model classes: Form, SavedQuery, WebResource, Workflow, PluginAssembly, PluginType, SdkMessageProcessingStep, StepImage, SecurityRole, AppModule, SiteMap, GenericComponent
- XmlWorkspaceReader expanded with 10 loaders (covers all 48 templates)
- XmlWorkspaceWriter with roundtrip-safe save for all types
- IComponentDefinition port (13 behavioral properties)
- Solution layering: ComponentLayer, LayerStack, SolutionLayerManager
- IComponentMerger interface + FormMerger using TreeMergeEngine
- MergeableNode: format-agnostic tree for merge operations (no XML dependency in core)
- Label data-loss bug fixed (multi-language support)
- 228 tests passing

## Milestone 3: MetadataRuntime + Solution Import

**Goal:** Replicate Dataverse solution framework behavior as a standalone runtime.

- [ ] `MetadataRuntime` facade - wires providers, layer manager, mergers, query API
- [ ] `ImportSolution(stream)` - deserialize ZIP, add layers, recompute active state
- [ ] `UninstallSolution(name)` - remove layers, cascade checks, recompute
- [ ] `GetEffective<T>(type, id)` - resolved component after layering
- [ ] `DependencyGraph` - component reference tracking, cascade validation
- [ ] `OnChanged` event - notify subscribers of component changes
- [ ] Incremental recomputation (only affected components, not full rebuild)
- [ ] `Serialization.Zip` package - solution ZIP pack/unpack

## Milestone 4: Workspace Manipulation API

**Goal:** Type-safe component manipulation for CLI and template engine.

### IWorkspaceContext interface
Abstraction layer for I/O — model never touches filesystem directly.

- [ ] Define `IWorkspaceContext` (read/write/delete/list/exists)
- [ ] `FileSystemContext` implementation (scripts, `dotnet new`, direct disk)
- [ ] `InMemoryContext` implementation (language server, tests — no disk)
- [ ] `TransactionalContext` implementation (CLI — buffered writes, rollback on failure)
- [ ] Migrate `XmlWorkspaceReader` / `XmlWorkspaceWriter` to `IWorkspaceContext`

### Dirty tracking
Write only modified files. `Load → Save` with no changes = zero git diff.

- [ ] Dirty flag per component
- [ ] `Workspace.GetModifiedFiles()` — list files to write
- [ ] Writer respects dirty flag — skips unmodified

### Component builders (fluent API)
- [ ] `EntityBuilder` — fluent API for entity + attribute creation
- [ ] `FormBuilder` — fluent API for form construction (add/remove/move control, add tab/section)
- [ ] Migrate template post-action scripts to typed API

### MergeableNode manipulation API
Extended API for editing the form body tree.

- [ ] `AddControl(sectionId, control)`, `RemoveControl(controlId)`, `MoveControl(controlId, targetSectionId, position)`
- [ ] `FindNode(predicate)`, `FindNodesByAttribute(attrName, pattern)` — tree search

## Milestone 5: CDN Snapshot Format

**Goal:** Fast, CDN-friendly serialization for SPA and WASM scenarios.

- [ ] Content-addressable component store (hash-based file names)
- [ ] Manifest + indices for filtered loading (by-type, by-entity, by-app)
- [ ] Binary serialization (MessagePack or custom) - no XML parser needed
- [ ] Sub-100ms cold start for typical solution
- [ ] `Serialization.Snap` package
- [ ] Version-tagged URLs for immutable CDN caching

## Milestone 6: Provider.Dataverse

**Goal:** Bidirectional sync with live Dataverse environments.

- [ ] `RetrieveMetadataChangesRequest` for entities/attributes/relationships
- [ ] `RetrieveMultiple` for forms, views, plugins, roles
- [ ] Incremental sync via server version tokens
- [ ] Drift detection (compare workspace vs live)
- [ ] Write metadata back (create/update entities, attributes)

## Milestone 7: Language Server

**Goal:** Real-time diagnostics and completions in VS Code.

### LSP server scaffold
- [ ] New project `TALXIS.Platform.Metadata.LanguageServer`
- [ ] LSP framework (OmniSharp or `Microsoft.VisualStudio.LanguageServer.Protocol`)
- [ ] Initialize: `textDocument/didOpen`, `textDocument/didChange`, `textDocument/didClose`
- [ ] Workspace load at startup (via `InMemoryContext`)

### Diagnostics
- [ ] XSD validation errors → LSP diagnostics with source location
- [ ] Duplicate GUID detection → diagnostics
- [ ] Dangling references (control → non-existent attribute) → warning
- [ ] Model-load errors from `Workspace.LoadErrors` → diagnostics
- [ ] Leverage `SourceLocation` from core model for precise positions

### Completions
- [ ] Entity logical names in lookups and references
- [ ] Attribute logical names in `datafieldname` attributes
- [ ] FormId / ViewId reference completions
- [ ] OptionSet values
- [ ] ClassId completions for control types

### Go-to-definition
- [ ] `datafieldname="ntg_vatvalue"` → jump to attribute definition in Entity.xml
- [ ] Lookup reference → target entity
- [ ] FormId reference → form file
- [ ] OptionSet reference → global/local option set definition

### File watching + incremental reload
- [ ] `workspace/didChangeWatchedFiles` handler
- [ ] Incremental reload — only changed files, not full workspace
- [ ] Leverage `InMemoryContext` for fast model update
- [ ] Invalidate diagnostics for changed files

### VS Code extension
- [ ] Extension manifest (`package.json`) — language ID for `.xml` in `Declarations/` context
- [ ] LSP client configuration
- [ ] Activation on `Solution.xml` detection in workspace

## Milestone 8: Code Generation

**Goal:** Generate typed code from metadata model.

- [ ] `CodeGen.CSharp` - Roslyn SyntaxFactory (not CodeDOM), split files per entity
- [ ] `CodeGen.TypeScript` - TS interfaces + Zod schemas
- [ ] Roundtrip: Entity.xml -> Core Model -> Entity.cs

## Related Repositories

| Repository | Role | Integration point |
|---|---|---|
| [TALXIS/tools-cli](https://github.com/TALXIS/tools-cli) | CLI commands, MCP server | References all packages |
| [TALXIS/tools-devkit-build](https://github.com/TALXIS/tools-devkit-build) | MSBuild SDK | References Validation |
| [TALXIS/tools-devkit-templates](https://github.com/TALXIS/tools-devkit-templates) | dotnet new templates | Post-actions will use Workspace API |
| INT0014-MetadataService | SPA metadata proxy | Will use core model as shared types |
| INT0014-EnvironmentDataService | Dataverse replacement runtime | Will use MetadataRuntime for solution import |
