# Metamodel composition architecture

Date: 2026-09-29
Status: design proposal for review; proposed types and APIs are not implemented unless explicitly marked existing.

## 1. Purpose

Provide one reusable metadata composition engine for local development, build validation, EDS import processing, and future language-server and portal consumers. A consumer selects a solution, usually Solution.UI, and loads its declared dependencies into a queryable model. Environment metadata supplies components unavailable in source projects and can be saved for offline use.

The materialized result is called **EffectiveMetadata**, not Default Solution. It is a view of the selected inputs. It is neither a solution archive nor an instruction to deploy into Dataverse.

This document specifies responsibilities, data contracts, processing order, persistence boundaries, and unresolved decisions. The implementation checklist remains in [the implementation plan](metamodel-implementation-plan.md). It refines the multi-source workflow in [Runtime Architecture](runtime-architecture.md); that document's broader runtime and uninstall APIs remain long-term goals.

## 2. Example and expected behavior

```text
Solution.UI
  -> Solution.Security
       -> Solution.DataModel
  -> Solution.DataModel
       -> Foundation.Model (NuGet package)

Solution.Logic -> Solution.DataModel
```

Opening UI loads Foundation.Model, DataModel, Security, and UI. DataModel appears once even though two paths reach it. Logic is excluded because it is not a UI dependency. Opening a deployment package is a different entry point and includes the package's selected solutions and their dependencies.

DataModel defines a table with name and phone. UI contributes a form for that table; an extension may contribute email. Queries see the available table, columns, and form together. Distinct columns survive partial upper table definitions. Changes to the same component follow its registered composition rules.

Editing a UI form creates or changes the UI source contribution. Reading dependencies never grants permission to modify their source files. Adding a column owned by DataModel requires selecting DataModel as the edit target; the entry solution and edit target are separate concepts.

### Direct references and transitive loading

Each project declares its direct dependencies through ProjectReference or PackageReference. The entry solution does not need to flatten the dependency graph or list every transitive dependency. For example, UI -> Security -> DataModel -> Foundation.Model loads all four solutions even if UI declares only Security. This is transitive dependency discovery, analogous to package dependency resolution; actual package versions still come from the build/restore result.

In the example above, UI also directly references DataModel because its forms use that model. That edge expresses direct usage, not a requirement to repeat every reachable dependency. Both paths resolve to one DataModel input. If Security gains another dependency, that dependency becomes reachable from UI without copying the reference into UI.

Solution.UI is a common entry point, not a special project name or mandatory global root. Any selected solution can be the root of a loading request. The resolver recursively follows references declared by each dependency, detects cycles and conflicts, and provides prerequisites before their consumers. The result must be the same whether a compatible shared dependency is reached through one path or several.

This keeps dependency ownership with the project that needs it and avoids maintaining duplicate dependency lists. The computed closure belongs to the loading plan; it is not written back as a flattened reference list into the entry project.

## 3. Existing implementation and required extensions

| Existing component | Responsibility retained | Extension required |
| --- | --- | --- |
| Workspace | Loaded components, solutions, memberships, source documents | Explicit source access; avoid treating mutable collections as the authoritative composed view |
| WorkspaceComponent | Metadata, ActiveState, Layers, Snapshots | Read-only effective projection and explicit target editing |
| ComponentSourceSnapshot | Source-owned component payload and provenance | Payload kind, stable source identity, revision and coverage |
| SolutionLayerManager / LayerStack | Registered component layers and resolution | Validated ordering input and explicit baseline handling |
| ComponentDefinitionRegistry and mergers | Identity and component-specific behavior | Nested-component composition and declared capability coverage |
| XmlWorkspaceReader / LoadMany | Parse source metadata and register contributions | Accept resolved inputs; retain original documents independently per source |
| XmlWorkspaceWriter / WriteSolution | Write source-owned metadata with original XML preservation | Consume only target changes and verify source revisions |
| IWorkspaceContext / TransactionalContext | Abstract document access and buffered writes | Conflict checks and defined recovery on partial commit failure |
| Existing validators | Check metadata and references | Receive dependency closure, baseline coverage and source diagnostics |

The XML identity correction is implemented in commit 5d45894: complete registered keys take precedence over name/position fallbacks. This is a local composition fix, not implementation of the architecture below.

## 4. Boundaries and dependencies

```text
CLI / build SDK / editor / EDS / portal
                 |
       MetadataSessionLoader
        /        |         \
Dependency   Source       Baseline
resolver     readers      provider
        \        |         /
         Normalized inputs
                 |
        CompositionPlanner
                 |
        MetadataComposer
                 |
         MetadataSession
       /         |          \
SourceStore  EffectiveMetadata  Diagnostics
       |
    EditSession -> ChangeSet -> source writer
```

The core metadata package remains format-independent and compatible with its existing dependency constraints. It contains identities, normalized contracts and component behavior. MSBuild evaluation, NuGet restore, Dataverse authentication/HTTP and SQL execution remain in adapters or consumers. No new package is required merely to introduce a type; place contracts in existing model/workspace assemblies first and separate external adapters when their dependencies require it.

The workspace orchestration layer coordinates reads and composition but has no implicit network access. A caller supplies adapters and an explicit load request. Queries never trigger background restores or environment requests.

## 5. Proposed types and contracts

Names are proposed. Prefer extending an existing type when it already has the required responsibility; do not create parallel layer stacks or source stores.

| Type | Main data or operations | Responsibility |
| --- | --- | --- |
| ComponentIdentity (existing) | Type and ObjectId | Stable component key, independent of solution membership and source path; nested identities must include owner context where required |
| SourceStore (proposed logical facade) | Source descriptors, component snapshots, original documents, revisions; lookup by source and component | Retain original contributions for diagnostics and target-only write-back; reuse existing storage |
| EffectiveMetadata (proposed read model) | Component lookup/enumeration, resolved values, provenance, coverage, session revision | Read-only materialized result of composition, distinct from editable source contributions |
| MetadataSessionLoader (proposed orchestrator) | Load(request) -> MetadataSession | Coordinate dependency resolution, readers, baseline loading, planning, composition and validation |
| MetadataLoadRequest | Entry point, configuration, mode, baseline selection, optional import order | Explicit reproducible loading inputs |
| ISolutionDependencyResolver | Resolve(entry, configuration) -> ResolvedSolutionGraph | Evaluate project/package edges through build tooling; no component merging |
| ResolvedSolutionGraph | Nodes, edges, resolved versions, source locations | Complete dependency closure with provenance |
| MetadataSourceDescriptor | Source ID, source kind, revision/hash, read-only flag | Identify one resolved input independently of its machine path |
| ISolutionMetadataReader | Read(descriptor) -> SolutionContribution | Adapt existing unpacked/archive readers to a common contract |
| SolutionContribution | Solution identity/version, publisher, managed state, component contributions | Preserve one solution's input before composition |
| ComponentContribution | Component key, source ID, payload kind, present properties, payload | Separate full definitions, segmented definitions and explicit changes |
| IMetadataBaselineProvider | Capture(request) -> MetadataBaseline | External live adapter, saved snapshot adapter, or supplied offline baseline |
| MetadataBaseline | Baseline ID, organization/version, capture time, coverage, effective definitions | Describe known starting metadata without fabricating solution-layer history |
| CompositionPlanner | Build(graph, baseline, mode, import context) -> CompositionPlan | Check order, overlap, coverage and supported operation semantics |
| CompositionPlan | Ordered contributions, baseline binding, diagnostics, fidelity | Explicit input to deterministic composition |
| MetadataComposer | Compose(plan, sources) -> EffectiveMetadata | Reuse component registry and existing layer engine |
| MetadataSession | Sources, graph, baseline, effective view, diagnostics, revision | Session lifetime and one consistent published state |
| EditSession | Target solution, base revision, pending ChangeSet | Isolated edits with explicit ownership |
| ChangeSet | Target-owned additions/updates/removals with preconditions | Input for validation, preview and write-back |

### Component identity

Use existing ComponentIdentity wherever sufficient. A nested column key must include its owning table; two columns named name in different tables are not one component. Preserve native IDs and source names as evidence. Cross-source identity resolution belongs to the registry/adapter, not fuzzy display-name matching. Conflicting ID/name mappings produce a diagnostic instead of silently unifying objects.

Source identity, NuGet package identity, solution identity, component identity and solution membership remain separate. The same component may belong to several solutions. A solution membership alone is not proof that the solution supplied the entire effective definition.

### Source and effective objects

SourceStore is a logical view over existing source snapshots and original-document storage. EffectiveMetadata publishes read-only projections or defensive copies; callers cannot mutate source payloads through it. A component query returns its effective value, contributing source IDs and capability/coverage status. Source snapshots remain available separately for diagnostics and editing.

An edit increments the session revision and invalidates affected projections. Initial implementation may rebuild the effective view; incremental caches are an optimization after correctness. A consumer observes one published revision, never a mixture of partially loaded inputs.

## 6. Loading workflow

1. Validate MetadataLoadRequest and select the mode described below.
2. Resolve the entry solution's evaluated project and package references. Include configuration and conditional build properties.
3. Detect cycles and missing inputs; retain a complete error chain. Resolve NuGet versions through the existing restore result.
4. Deduplicate identical source revisions. If different inputs claim the same solution identity with different versions/content, report a conflict. No implicit local-over-package winner.
5. Parse each solution through existing readers. Record original documents and source-owned payloads before composition.
6. Load the explicitly selected baseline, if any, through the supplied provider. Record its coverage and revision.
7. Build a composition plan: prerequisite order, parent reference-list precedence, additional import context, managed/unmanaged semantics, payload kinds and baseline overlap checks.
8. Compose components using registered rules. Compose table children independently so a partial table contribution cannot erase unrelated columns.
9. Run existing validators against the resulting context. Distinguish missing components from unknown baseline coverage.
10. Publish a MetadataSession with either a valid result or explicitly incomplete diagnostics. Fatal ordering/identity ambiguity prevents a result being labelled authoritative.

MSBuild references and component relationships are separate graphs. References declare available sources; component relationships validate actual usage against those sources. Shared dependency loading does not imply automatic deployment.

### 6.1. Reference-list precedence for local composition

When two dependencies of the same parent contribute conflicting definitions, the dependency listed earlier in the parent's project references has higher precedence. For references A followed by B, A wins a supported conflicting value. With a later-applied-wins merger, compose B before A, then apply the parent's own contribution. Non-conflicting components from both remain available; component-specific merge rules still apply.

Record each reference's order on its graph edge. Use the active evaluated reference list for the selected build configuration, preserving declared order rather than sorting by name, path or package kind. Imported or generated references must expose a reproducible effective order through the build adapter. This rule applies equally to resolved project and package dependencies; the adapter must preserve their relative declaration order.

Dependency prerequisites remain mandatory: a dependency is processed before its consumer. If reference precedence contradicts a prerequisite (for example A depends on B but B is declared to win over A), report inconsistent ordering constraints rather than silently reversing the dependency. At each parent, direct sibling order is defined; a shared transitive node is still loaded once. Conflicting ordering constraints from several parents and priority between different transitive branches require an explicit consistent graph policy and remain open; do not resolve them by incidental traversal order.

This is the local project composition policy. It does not assert that Dataverse reads project files, change managed/unmanaged semantics, or override known runtime import history. Environment inspection and import simulation retain their explicit modes and context.

Acceptance: swapping two independent sibling references reverses the winning supported property; both retain non-conflicting components; the parent remains above its prerequisites; shared dependencies are not duplicated; contradictory constraints are diagnosed.

## 7. Composition modes and environment overlap

These modes are proposed to make the local/remote contract explicit. They require design review before implementation.

### 7.1. Project composition

Inputs: a selected solution dependency closure, optionally a compatible baseline that precedes those contributions.

Purpose: local editing and validation without an environment connection. Dependency order determines prerequisites. Conflicting direct dependencies of one parent use reference-list precedence: earlier references have higher priority (section 6.1). More complex graph conflicts require consistent ordering constraints; arbitrary stable sorting does not resolve them.

A supplied baseline must declare its scope. A captured environment containing the same customized solutions is not automatically a clean system baseline. A generic fresh-environment snapshot also depends on platform version and installed applications; it is not assumed universally compatible.

### 7.2. Environment inspection

Inputs: live capture or a saved capture of effective environment metadata.

Purpose: inspect current known environment state. Local inputs may be shown alongside it for comparison, but are not replayed on top by default. An effective snapshot is stored as a baseline, not registered as a fictional managed solution or a complete layer stack.

This is the smallest proposed vertical slice for proving live/offline support: capture supported table metadata, materialize it, save it, reopen it offline and compare supported properties.

### 7.3. Local change preview

Inputs: an environment baseline plus explicitly targeted local changes.

Purpose: show a proposed result without modifying the environment. This is distinct from claiming exact Dataverse import simulation.

If local sources overlap installed solutions, a preview must not replay all local files over the already composed environment. Two paths are possible:

- For an initial limited preview, require a recorded common source revision and compute explicit local changes relative to it. Apply only supported changes and label the output as a development preview.
- For exact supported import simulation, require sufficient source-layer and import-operation information to replace/update the appropriate contributions. An effective metadata capture alone is insufficient.

Without enough evidence, return an overlap diagnostic and preserve separate local and environment views. Do not silently discard environment customizations or guess an import result.

### Worked overlap example

The captured environment already contains DataModel version 1 and a separately installed extension adding phone. The local project modifies a name property in DataModel version 2. Replaying an entire partial DataModel table could lose phone; replaying a form snapshot could apply existing changes twice.

With a known common source revision, a limited preview applies the explicit name-property change and preserves phone. A deletion, managed-property conflict or unsupported form operation requires richer context or a diagnostic. A package version match alone does not prove that its full payload equals the environment's effective state.

## 8. Layering contract

Component-level rules must align with verified Dataverse behavior. Managed inputs and the shared active/unmanaged layer retain their distinct semantics; multiple local unmanaged projects remain separate source contributions to one active layer. Reference type does not determine layer strength. See [Microsoft's layer model](https://learn.microsoft.com/en-us/power-platform/alm/solution-layers-alm).

CompositionPlanner supplies validated ordering to SolutionLayerManager. MetadataComposer extends existing per-type rules rather than introducing another engine. Full, segmented and delta inputs have explicit contracts; omission from segmented input is not removal. Changes must preserve property presence, localization and agreement between typed properties and XML bodies.

A materialized environment baseline needs an explicit composition path around the existing manager until its baseline contract is implemented. Do not force it into a System or Default layer and imply historical accuracy. Layer origin is known only where the input actually supplies it.

Initial scope excludes exact patch, staged upgrade and uninstall simulation. Unsupported operations fail with a capability diagnostic. Duplicate-key diagnostics and broader component conformance remain separate tasks beyond the completed XML identity fix.

## 9. Baseline acquisition and offline behavior

### Initial live capture

Implement the provider outside the core library. Use caller-supplied authentication and capture a declared subset: tables, columns and relationships first. Schema metadata retrieval/cache facilities support this subset; forms, apps, ribbons, roles and PCF require their own readers and coverage checks. A table metadata response is not a full environment dump.

Capture published metadata for the first milestone. Record this choice; unpublished customization support requires a separate contract. Preserve native metadata identifiers, logical names, types, required properties and supported relationships. Keep raw provider responses separate if necessary for lossless refresh; normalize only supported fields with explicit unknowns.

The snapshot envelope contains schema version, provider version, organization identity, capture timestamps, platform version when available, query/coverage description, native metadata IDs, and any valid refresh token. A refresh token is scoped to its organization and query. Apply incremental additions and deletions; rebuild the relevant cache when its token expires. These mechanisms follow [Dataverse schema caching guidance](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/cache-schema-data).

Do not store credentials, access tokens or business records in the metadata snapshot. Solution inventories and metadata can be captured at different times: record consistency limits and do not claim a transactional full-environment snapshot.

### Offline selection

The caller chooses one of: a pinned saved capture, a versioned supplied baseline, or no baseline. Offline mode performs no network requests. If a requested live refresh fails, using an older snapshot requires an explicit fallback policy and emits a stale-baseline diagnostic.

The default proposed snapshot format is versioned JSON through a serialization adapter, subject to the library's package constraints. Avoid a custom binary format in the first milestone. CDM is an optional coverage source to evaluate, not a substitute for a complete Dataverse baseline.

Without baseline coverage, local composition can still operate on known components. Validation reports unknown external references as incomplete coverage rather than certifying a complete successful environment validation.

## 10. Editing and persistence workflow

1. Begin EditSession with an editable target solution and the session revision.
2. Read effective state to resolve references; modify a detached target-owned contribution.
3. Record a ChangeSet. For an inherited component, derive a supported segmented contribution or change representation; reject unsupported edits rather than copying the whole effective object.
4. Recompose a preview and run validation using the same dependency/baseline context.
5. Generate a target-only write preview using existing writer capabilities.
6. Check that source hashes/revisions still match the loaded state. Report a conflict if external tools changed the files.
7. Persist through the existing document/context abstraction and publish the new revision only after success.

Existing TransactionalContext buffers writes but does not guarantee atomic multi-file filesystem commit: failures during Commit can leave partial changes. Initially validate before writing and define recovery/backups for affected files; do not advertise database-style rollback. Package archives and NuGet cache inputs remain read-only.

Acceptance: no-op save has zero byte diff; editing UI leaves DataModel unchanged; discarded edits affect neither sources nor effective state; an external file change prevents stale overwrite. Saving local changes does not import anything into Dataverse.

## 11. First vertical slices and acceptance checks

### Slice A: environment capture and offline materialization — proposed next architecture milestone

Problem: the model cannot rely on live access, but source repositories do not contain all standard metadata.

Solution: implement the baseline contract and a minimal table/column/relationship provider plus snapshot reader/writer. Load the capture into an inspection session without mixing in local solutions.

Checks: saved capture reopens offline with equivalent supported metadata; no HTTP requests in offline mode; unknown coverage is visible; credentials are absent from snapshots. Use a recorded/synthetic provider response for automated tests, then a separately configured development environment for a live smoke test. No environment mutation is required.

### Slice B: dependency closure and project composition

Problem: caller-supplied LoadMany order does not discover references or enforce prerequisite constraints.

Solution: resolve the four-solution example through the build adapter, normalize inputs and compose through existing layers.

Checks: UI includes Security/DataModel once and excludes unrelated Logic; a UI -> Security-only declaration still discovers DataModel and its dependencies; adding a dependency to Security needs no duplicate UI reference; one-path and multi-path loading produce equivalent results; a non-UI entry solution works; cycle/conflict diagnostics are reproducible; partial UI table definitions preserve DataModel columns.

### Slice C: isolated editing and supported previews

Problem: a composed mutable object can lose source ownership or overwrite inherited state.

Solution: implement explicit target edits, revision checks and source-only persistence. Add environment previews only for the agreed supported change contract.

Checks: no-op byte identity, isolated target changes, discarded edits, stale-source rejection and overlap diagnostics. Same-form layers use one stable form ID; different forms are not mistaken for a layering test.

### Slice D: EDS adapter

Problem: EDS has its own composition pipeline and requires a concrete schema representation.

Solution: map the shared effective result into the existing EDS schema pipeline.

Checks: small import sequences produce expected EDMX; supported version updates are repeatable; the library works without SQL or EDS hosting.

These slices are proposed sequencing for design review. They do not silently replace the broader implementation checklist or authorize environment access without configuration.

## 12. Decisions to review

| Decision | Proposed default | Remaining question |
| --- | --- | --- |
| Materialized representation | EffectiveMetadata | Final public API names can be chosen during implementation |
| Local entry point | Selected solution, typically UI | Deployment package remains a separate entry point |
| Live/local combination | Explicit modes, no automatic replay of overlapping sources | Required fidelity and available layer/source evidence for first preview |
| Initial environment scope | Published tables, columns, relationships | Which additional components are needed first by EDS/portal? |
| Offline fallback | Pinned captured baseline with declared coverage | Who produces/version-controls a distributable clean baseline? |
| Direct sibling conflicts in local composition | Earlier parent reference has higher precedence | Reconcile constraints across several parents and transitive branches; preserve real import context in other modes |
| Source editing | Explicit editable target, detached changes, revision checks | Per-component segmented write representation |
| First milestone | Live capture -> effective inspection -> offline reload | Confirm prioritization relative to local dependency loading |

No claim of exact Dataverse import simulation is made from an effective environment dump alone. The deliverable for this design is a reviewable contract that makes the required evidence and supported behavior explicit.
