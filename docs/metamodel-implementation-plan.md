# Metamodel implementation plan

Updated: 2026-09-25
Status: proposed implementation plan

## Purpose and scope

Extend the existing metadata library so consumers can load a solution and its dependencies, calculate effective component state using Dataverse layering rules, validate that state, and save edits to the intended source solution. The same composition logic should support development tools and the Environment Data Service (EDS).

This plan covers improvements to an existing implementation. Checked items describe available foundations; unchecked items describe remaining work. Existing layer classes and passing examples do not establish complete Dataverse compatibility. Reproduce previously identified failure cases on the implementation baseline before changing behavior.

### Terms

| Term | Meaning |
| --- | --- |
| Solution | A Dataverse customization container containing components such as tables, columns, forms, applications, and security roles. |
| Source contribution | The metadata supplied by one solution for a component. |
| Effective state | The component definition obtained after applying the relevant contributions and layering rules. |
| Target solution | The editable solution that receives a change. |
| Dependency closure | A solution's direct and transitive dependencies. |
| Baseline | Metadata available before applying local changes, obtained from an environment, a saved snapshot, or an offline distribution. |
| ProjectReference | A dependency on a local build project. |
| PackageReference | A dependency resolved through NuGet. A package can contain solution metadata or other build artifacts. |
| PDPackage | A deployment package containing solutions and deployment configuration. It is distinct from an individual solution archive. |
| PCF | Power Apps Component Framework; custom controls whose manifests describe properties and datasets. |
| EDS | Environment Data Service; a separate consumer that imports metadata and uses it to maintain its own schema. |
| EDMX | The XML model representation used in the EDS schema-generation pipeline. |

The effective model is analogous to a Default Solution view of the loaded components. It is not a new deployable solution and does not necessarily contain every component in an environment.

## 0. Existing foundations and architectural constraints

- [x] Component layer storage and resolution through `SolutionLayerManager`, `LayerStack`, and `ComponentLayer`.
- [x] Managed layers, a shared active/unmanaged layer, ordering, and source information.
- [x] Specialized composition handlers for forms, site maps, applications, and ribbons.
- [x] XML tree changes using added, modified, and removed operations.
- [x] Multi-source loading through `XmlWorkspaceReader.LoadMany`, using caller-provided order.
- [x] Access to source metadata, effective state, layer information, and source snapshots through `WorkspaceComponent`.
- [x] Validation integrated with build tooling.
- [x] Archive and PCF readers, with component coverage to be assessed individually.

Requirements:

- Support both local project dependencies and package dependencies using the same metadata composition semantics.
- Reuse the existing layer, reader, writer, and validation infrastructure.
- Keep the core model compatible with `netstandard2.0` and the repository's dependency constraints. MSBuild evaluation, package restoration, HTTP access, and SQL execution belong in consumer tooling or adapters rather than the core model.
- Preserve unknown source content and roundtrip safety.
- Track compatibility by component type and supported operation.

## 1. Reading and editing contracts

### 1.1. Separate source metadata from effective state

Relevant areas: workspace access, component access, readers, writers, builders, and validators. Builders are the operations that construct or modify metadata components.

- [ ] Inventory consumers of entity collections, source metadata, and effective component state.
- [ ] Classify each consumer as requiring a source contribution or the composed result.
- [ ] Ensure builders can resolve components from dependencies while retaining an explicit target solution for edits.
- [ ] Ensure validators use the available composed context and writers use target-owned changes.
- [ ] Define explicit access to source and effective representations, reusing existing APIs where sufficient.
- [ ] Document mutation ownership and preserve compatibility when changing existing API behavior.

Acceptance: a caller can inspect lower and upper contributions and the effective result separately, and can determine which representation may be edited.

### 1.2. Prevent accidental changes to dependencies

- [ ] Check whether source and effective representations share mutable objects or collections.
- [ ] Prevent editing the effective view from mutating a dependency's source contribution.
- [ ] Require a target solution for component creation and modification.
- [ ] Represent supported edits to inherited components as contributions in the target solution.
- [ ] Invalidate or recompute effective state after changes.

Acceptance: a base solution provides `name` and `phone`, and an extension adds `email`. Adding `website` to the extension produces four effective columns while leaving the base source unchanged.

## 2. Solution layering

References establish prerequisite order. If UI depends on Model, Model must be available before UI is applied. Project and package references have the same dependency meaning; the source format does not determine layer strength.

```text
Dependencies: UI -> ApplicationModel -> BaseModel
Prerequisite order: BaseModel -> ApplicationModel -> UI
```

A dependency graph does not completely order independent solutions, replace import history, or override managed/unmanaged rules.

### 2.1. Preserve columns when composing table contributions

Relevant areas: workspace component merging, nested component identity, and layer resolution.

Failure case to reproduce: replacing an entire table object with an upper partial definition loses columns supplied by the lower solution.

- [ ] Reproduce loading a model solution followed by a UI solution that includes the same tables without column definitions.
- [ ] Identify whole-object replacement paths and paths that invoke component composition.
- [ ] Define nested component identity using both the owning table and the column identity.
- [ ] Preserve lower columns absent from a partial upper definition.
- [ ] Apply supported property overrides when both contributions describe the same column.
- [ ] Treat omission in a partial definition as absence of a contribution, not deletion.
- [ ] Verify identifier comparison, including case handling and identical column names in different tables.

Acceptance: all base columns remain after loading the UI solution; new upper columns are added; matching columns follow their property rules without affecting another table.

### 2.2. Match XML nodes by identity

Relevant area: `TreeMergeEngine`, including child matching and insertion.

Failure case to reproduce: a sole same-type child or a child at the same position is selected despite having a different explicit identifier.

- [x] Reproduce a lower node with ID A and an upper node of the same type with ID B.
- [x] Match stable keys before using count or position heuristics.
- [x] Do not fall back to positional matching after an explicit key fails to match.
- [ ] Define the applicable key for each supported XML context.
- [ ] Diagnose duplicate or ambiguous keys.
- [x] Ensure insertion does not overwrite an unrelated sibling.

Acceptance: A and B remain distinct; editing B affects only B; repeated supported application does not introduce unintended duplicates.

### 2.3. Distinguish full definitions, partial definitions, and change sets

- [ ] Document the representation provided by each unpacked, archive, and EDS input reader.
- [ ] Explicitly identify whether an input is a full definition, a segmented contribution, or an operation-based change set.
- [ ] Verify handling of nodes without added, modified, or removed markers.
- [ ] Process snapshot additions according to the input contract rather than silently skipping them.
- [ ] Apply deletion only when the input and import operation define deletion semantics.
- [ ] Report unsupported or ambiguous representations rather than returning a silently altered result.

Acceptance: equivalent supported snapshot and change-set inputs produce equivalent state; omissions in partial inputs do not delete inherited components.

### 2.4. Preserve intermediate-layer changes

- [ ] Add a three-layer case where the middle layer changes a property and the top layer omits it.
- [ ] Replace composition paths that inspect only the first and last contributions and lose intermediate changes.
- [ ] Distinguish an absent property, an empty value, and an explicit reset where the input format supports that distinction.
- [ ] Verify localized labels independently for each language.
- [ ] Keep application XML and typed component and role collections consistent after composition.

Acceptance: the middle-layer change remains effective, and XML and typed accessors describe the same result.

### 2.5. Verify component-specific behavior

- [ ] Forms: tabs, sections, rows, cells, controls, ordering, supported moves, and conflict handling.
- [ ] Site maps: areas, groups, subareas, identity, order, and supported move/delete operations.
- [ ] Applications: included components and role bindings, with consistent XML and typed collections.
- [ ] Ribbons: supported input representations and actual change scenarios handled by the existing merger.
- [ ] Choices: option identity by value and the need for specialized composition.
- [ ] Security roles: distinct rules for custom and predefined roles.
- [ ] Provide small lower/upper input examples and expected output for each claimed behavior.
- [ ] Publish an explicit list of unsupported operations.

Acceptance: each supported component behavior has an executable example and expected result. Compatibility claims are limited to verified scenarios.

### 2.6. Connect dependency ordering to the existing layer manager

- [ ] Feed dependency-derived order into existing source registration and layer resolution.
- [ ] Preserve the shared active layer and source provenance for unmanaged contributions.
- [ ] Define additional ordering context for independent solutions that modify the same component, such as deployment import order.
- [ ] Diagnose unresolved ordering ambiguity rather than using incidental traversal order.
- [ ] Keep the aggregate Default Solution-like view separate from importable layers.
- [ ] Distinguish removing an input source from simulating solution uninstall.
- [ ] Define the initial lifecycle operation scope; implement and test update, upgrade, patches, and uninstall separately where required.

Acceptance: prerequisites precede extensions, active-layer behavior is preserved, and unresolved conflicts between independent inputs are visible.

## 3. Loading a solution and its dependencies

### 3.1. Local project dependencies

- [ ] Accept a selected solution project as the editing entry point.
- [ ] Obtain evaluated references, including conditions, imported build properties and targets, and configuration-specific behavior.
- [ ] Resolve relative paths from the declaring project.
- [ ] Distinguish solution projects from plugin, script library, code application, and PCF projects.
- [ ] Traverse direct and transitive dependencies.
- [ ] Load a shared project once even when reached through multiple paths.
- [ ] Report missing projects with the declaring project and reference.
- [ ] Report cycles with the complete dependency chain.
- [ ] Return identified sources and prerequisite order to the metadata reader and layer manager.

Acceptance: loading UI with dependencies on Security and DataModel includes those solutions once. An unrelated Logic solution is not loaded merely because it is nearby or belongs to the same deployment package.

### 3.2. Package dependencies

- [ ] Reuse existing build SDK package-resolution mechanisms.
- [ ] Use restored package versions and resolved locations rather than selecting arbitrary cache entries.
- [ ] Distinguish solution or PCF packages from ordinary .NET dependencies.
- [ ] Identify solutions within packages, including packages containing several solutions.
- [ ] Resolve transitive dependencies and verify their publication behavior, including dependency suppression during packing.
- [ ] Track NuGet package identity/version separately from contained solution identity/version.
- [ ] Diagnose incompatible versions and duplicate representations of the same solution.
- [ ] Normalize package metadata to the same composition contract as local project metadata.
- [ ] Keep restoration outside builders and define behavior when required restored inputs are unavailable.

Acceptance: shared package dependencies are loaded once; missing or conflicting inputs produce actionable diagnostics; equivalent local and package inputs produce equivalent effective state.

### 3.3. Unpacked projects, solution archives, and deployment packages

- [ ] Inventory component coverage of existing package, archive, and XML readers.
- [ ] Retain source-document ownership for unpacked inputs.
- [ ] Read packed manifests and customization metadata without assuming an unpacked directory layout.
- [ ] Extract solutions and available import ordering from PDPackage configuration.
- [ ] Do not derive import order from archive entry order.
- [ ] Retain provenance for project/package/archive, solution, component, and source document.
- [ ] Keep metadata loading separate from executing deployment code embedded in packages.

Acceptance: supported packed and unpacked representations yield equivalent metadata; deployment inputs compose without duplicate dependencies.

### 3.4. PCF metadata

- [ ] Connect existing file, project, and archive manifest readers to dependency loading.
- [ ] Preserve control identity and provenance, including multiple controls in one input.
- [ ] Expose property types, required flags, defaults, and datasets to consumers.
- [ ] Resolve form control and parameter references against loaded descriptions.
- [ ] Diagnose a missing requested control rather than selecting an arbitrary available control.

Acceptance: existing readers resolve the requested control and its parameters from a dependency without introducing a parallel parser.

## 4. Writing changes to the target solution

### 4.1. Source-aware persistence

- [ ] Review existing document keys, source snapshots, and change tracking.
- [ ] Use component identity together with source solution/document identity where needed for write-back.
- [ ] Write only target-owned changes to the editable solution.
- [ ] Avoid serializing the entire composed state into every source project.
- [ ] Avoid copying inherited columns into the target automatically.
- [ ] Keep dependency archives and the package cache read-only.
- [ ] Verify that reloading saved changes reproduces the edited effective state.
- [ ] Preserve unknown XML elements and attributes throughout the roundtrip.

Acceptance: load/save without edits produces zero byte differences. A target change affects only the appropriate target documents. Persistence tests use disposable copies of inputs.

### 4.2. Compatibility with template builders

- [ ] Compare the editing contract with migrated template builders.
- [ ] Identify builders that need explicit target ownership or effective dependency context.
- [ ] Keep architecture changes separate from unrelated template migrations.
- [ ] Re-run existing template scenarios after integrating the new contracts.

Acceptance: builders continue generating expected source content while using dependencies for reading and validation.

## 5. Validation across dependencies

- [ ] Review current single-solution and deployment-package validation contexts.
- [ ] Supply existing validators with the selected solution and its dependency closure.
- [ ] Remove the requirement for a shared PDPackage when validating references between dependent solutions.
- [ ] Resolve relationship tables and columns throughout the available context.
- [ ] Validate application role bindings and form component references within supported validator coverage.
- [ ] Distinguish a missing dependency from a missing component inside a loaded dependency.
- [ ] Do not conceal a missing reference by loading every neighboring project.
- [ ] Validate prohibited component changes independently of composition; an upper layer does not make arbitrary column type changes valid.
- [ ] Include solution, component, and source location in diagnostics where available.
- [ ] Report incomplete baseline coverage when it prevents a complete check.

Acceptance: a UI role reference resolves through its declared Security dependency. Removing the dependency exposes the problem. Missing relationship targets are diagnosed regardless of deployment-package membership.

## 6. Live and offline metadata baselines

The model needs standard tables and components absent from local projects. Live connectivity must remain optional. Precedence between live state, saved snapshots, and proposed local changes requires an explicit contract.

### 6.1. Baseline provider contract

- [ ] Define a common input contract for environment metadata, saved snapshots, and an offline baseline distribution.
- [ ] Distinguish current environment state from the proposed result of local changes.
- [ ] Retain baseline version, provenance, and component coverage.
- [ ] Reconcile components represented in both the baseline and loaded solutions without duplicate identity or repeated application.
- [ ] Define freshness rules for live versus cached data and composition rules for local edits.
- [ ] Distinguish a snapshot of effective metadata from complete solution-layer history.

Acceptance: a local project can resolve available standard components offline, and consumers can identify coverage gaps.

### 6.2. Baseline implementations

- [ ] Define the initial required metadata coverage: tables, columns, relationships, and additional components used by supported scenarios.
- [ ] Define snapshot acquisition, storage format, and versioning.
- [ ] Evaluate Common Data Model (CDM) schemas for specific coverage rather than assuming a complete fresh Dataverse snapshot.
- [ ] Use Metadata Browser as an inspection reference, not as an import or layering engine.
- [ ] Define refresh and cache invalidation behavior.
- [ ] Diagnose component types unsupported by a provider.

Acceptance: equivalent supported live data and saved snapshot data produce equivalent baselines. Environment-connected tests are separate from offline tests.

## 7. EDS integration

EDS currently parses imported solution metadata, builds an active representation, generates EDMX, and applies changes to its own SQL schema. The shared library should own metadata reading and composition; EDS should retain its service and storage responsibilities.

### 7.1. Integration boundary

- [ ] Confirm the active EDS host and import API entry point.
- [ ] Document the pipeline from import through parsing, active-state composition, EDMX generation, schema comparison, and SQL execution.
- [ ] Identify the parsing and active-state construction steps to replace with shared library calls.
- [ ] Keep HTTP handling, import history, SQL migrations, and server lifecycle in EDS.
- [ ] Maintain dependency direction from EDS to the shared library.
- [ ] Review the integration against the EDS technical design.

Acceptance: the replacement boundary and call sequence are explicit; CLI and metadata composition work without an EDS server.

### 7.2. Effective-state adapter

- [ ] Map tables, columns, types, nullability, choices, and relationships to the representation used for EDS EDMX generation.
- [ ] Identify metadata required by EDS but not yet retained by the shared model.
- [ ] Preserve version and import sequence information required for composition.
- [ ] Replace EDS-specific composition with an adapter over the common implementation.
- [ ] Compare generated EDMX on small fixtures before executing SQL migrations.
- [ ] Define initial import scope separately from plugin execution, deployment actions, and business-data handling.

Acceptance: the shared model and EDS schema representation agree for supported inputs; repeated imports and version updates have explicit tests.

## 8. Reproducible integration scenarios

Use a four-solution example with these responsibilities and references:

| Solution | Responsibility | Solution dependencies |
| --- | --- | --- |
| DataModel | Tables, columns, and relationships | None in this example |
| Security | Application security roles | DataModel |
| UI | Forms and an application bound to two Security roles | DataModel, Security |
| Logic | Plugin-related components | DataModel |

The deployment package contains all four solutions. UI can include partial table definitions without repeating DataModel columns. Test inputs must declare their expected column identities and counts so checks do not depend on a private machine or an external working directory.

| Scenario | Expected result |
| --- | --- |
| Evaluate references | The graph above is resolved without cycles. |
| Resolve application roles | Both role identifiers match actual Security definitions. |
| Load UI | UI, Security, and DataModel are available; Logic is excluded. |
| Reach a shared dependency twice | DataModel is loaded once. |
| Compose DataModel and UI | All base columns remain; upper additions are present. |
| Load Logic | DataModel is available and plugin inputs retain their distinct type. |
| Load the deployment package | All four solutions are available with import context and no duplicates. |
| Save without edits | Source bytes remain unchanged. |
| Edit a UI-owned contribution | Only UI-owned source content changes. |
| Override one column property | A supported upper property takes effect without losing unrelated properties. |
| Compose one form across layers | The inputs use the same form ID and produce the expected merged structure. |
| Compare packed and unpacked inputs | Supported metadata is equivalent. |

- [ ] Make the scenario inputs and expected outputs reproducible from repository-accessible fixtures or documented setup.
- [ ] Add missing-reference, cycle, and identity/version-conflict cases.
- [ ] Add regression cases for sections 2.1 through 2.4.
- [ ] Build the changed sources and run relevant tests against that build.
- [ ] Define a small Dataverse comparison suite for the component behaviors being claimed; keep tests that import into an environment separate.

Acceptance: another contributor can reproduce the checks without access to a specific developer's filesystem.

## 9. Build SDK and deployment packaging

These tasks require coordinated changes in build and deployment consumers, separate from core metadata composition.

- [ ] Verify solution-to-solution references during builds, including ordering, outputs, and packaging.
- [ ] Test actual build behavior; disabling assembly output references alone does not establish solution dependency support.
- [ ] Verify publication and restoration of transitive solution package dependencies.
- [ ] Construct a deployment set containing each shared dependency once.
- [ ] Deduplicate using the appropriate identity: package identity and contained solution identity are distinct.
- [ ] Ensure deployment tooling imports each shared solution once, before its consumers.
- [ ] Define the deployment container separately from the standard solution archive format; do not assume nested solution archives are supported.

Acceptance: UI and Security share a Model dependency that is packaged once and imported before either consumer.

Out of scope for this work: renaming the Workspace abstraction.

## Immediate milestone: 2.2. Match XML nodes by identity

Status: completed for authoritative key matching on 2026-09-25. Duplicate-key diagnostics and the broader per-context key audit remain open in section 2.2.

Problem: XML composition may select a same-type child by count or position even when its explicit ID differs. A change intended for node B can therefore modify or remove node A.

Proposed solution: treat an explicit identity key as authoritative. Resolve that key before structural heuristics; a keyed lookup with no match must not fall back to a different node by position. Retain positional matching only for supported keyless structures. Verify insertion separately so new nodes do not overwrite unrelated siblings.

Implementation sequence:

1. Reproduce the mismatched-ID case on the current implementation baseline.
2. Add regression coverage for modification and removal with different IDs, successful matching IDs, and supported keyless structures.
3. Apply the smallest correction to matching and verify insertion behavior.
4. Build from source and run the relevant tree and component composition tests.
5. Report the observed failure, behavior change, test results, and any remaining scope within section 2.2.

Acceptance: an operation targeting B never modifies or removes A; valid matching-ID operations and supported keyless behavior continue to work. Define missing-target behavior from the operation contract rather than inventing a match.

This increment does not require dependency-loader, SDK, EDS, or environment deployment changes. The broader delivery sequence below remains the roadmap after this bounded fix.
Verification results:

- Before the fix, 10 of 18 new regression cases failed, including mismatched-ID operations on a Warehouse application form.
- After the fix, all 57 focused identity, tree merge, form merge, component merge, and layering cases passed.
- The complete test project passed: 738 tests, no failures or skips, using a freshly built assembly.
- The repository includes a Warehouse form fixture; tests construct upper contributions in memory. The original sandbox input remains unchanged.
- The first complete registered key set is authoritative in merge and diff. Secondary keys remain available when higher-priority keys are absent. Existing missing-target modification/removal behavior remains a no-op.
- Existing unrelated nullable-reference and test-analyzer build warnings remain; no new warnings originate in the changed files.

## 10. Delivery order

1. **Reading and editing contracts:** section 1. Identify source/effective access and mutation ownership.
2. **Component preservation and persistence:** sections 2.1, 2.4, and 4.1. Preserve columns and intermediate changes; verify zero-diff saves.
3. **XML identity and input semantics:** sections 2.2 and 2.3. Verify keyed matching and explicit snapshot/change-set handling.
4. **Dependency loading and validation:** sections 3.1, 3.2, 2.6, and 5. Resolve the closure and diagnose missing inputs, cycles, and invalid references.
5. **Input and component coverage:** sections 2.5, 3.3, and 3.4. Verify archive, deployment-package, and PCF scenarios and document supported operations.
6. **EDS adapter:** section 7. Reuse common composition while preserving EDS infrastructure responsibilities.
7. **Baseline providers:** section 6. Define the contract early; implement live and offline providers as a separate stage.
8. **SDK, deployment, and builder integration:** sections 9 and 4.2. Deliver dependencies without duplication and preserve builder behavior.

Each stage is complete when its declared operations are implemented, its acceptance checks pass, and task status is updated. Layering compatibility is recorded per component type and operation.

## 11. Open design decisions

- Precedence and freshness rules for live metadata, cached baselines, and local edits.
- Ordering of independent solutions that modify the same component.
- Initial lifecycle coverage: import, update, upgrade, patches, and uninstall.
- Standard-component coverage of an offline baseline.
- Active EDS hosting/import path and alignment with its technical design.

Target API contract: loading a set of solutions produces metadata state corresponding to their import into Dataverse, within explicitly supported component types and operations.

## References

- [Solution layers](https://learn.microsoft.com/en-us/power-platform/alm/solution-layers-alm)
- [How managed solutions are merged](https://learn.microsoft.com/en-us/power-platform/alm/how-managed-solutions-merged)
- [Microsoft Common Data Model](https://github.com/microsoft/CDM)
- [Metadata Browser](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/browse-your-metadata)
