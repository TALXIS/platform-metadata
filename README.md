# TALXIS Platform Metadata

An open-source metadata model for model-driven platforms. Provides typed C# representations of entities, attributes, forms, views, option sets, solutions, and solution layers - compatible with Dataverse and Power Platform.

## What this is

A shared kernel that defines **what platform components are and how they behave**. It doesn't store data, execute plugins, or run workflows - it's the type system that all of those depend on.

## Who uses it

| Consumer | How |
|---|---|
| [TALXIS CLI](https://github.com/TALXIS/tools-cli) | Workspace validation, component scaffolding, language server |
| [TALXIS Build SDK](https://github.com/TALXIS/tools-devkit-build) | Build-time schema validation, version stamping |
| [TALXIS Templates](https://github.com/TALXIS/tools-devkit-templates) | Post-action scripts for safe workspace manipulation |
| Future: runtime services | Metadata management, solution import/export, query planning |

## Capabilities

### Metadata Model (`TALXIS.Platform.Metadata`)
Typed representations of platform component metadata:
- **Entity** - table definitions with ownership, activity type, audit settings
- **Attribute** - column definitions with types, constraints, option sets
- **Relationship** - 1:N, N:1, N:N with cascade behavior
- **OptionSet** - global and local choice definitions with localized labels
- **Form** - form XML structure (tabs, sections, controls)
- **View** - saved queries with FetchXML and layout
- **Security Role** - privilege definitions
- **Plugin/Workflow** - registration metadata

### Solution Model (`TALXIS.Platform.Metadata.Solutions`)
- **Solution** - component registry, publisher, version
- **Component definitions** - per-type behavior (mergeable, overwritable, dependency rules)
- **Layers** - managed/unmanaged stacking, active layer resolution, merge behavior

### Serialization (`TALXIS.Platform.Metadata.Serialization`)
- **SolutionPackager format** - bidirectional XML ↔ model mapping
- **Roundtrip fidelity** - Load → Save with no changes = zero git diff
- **Minimal writes** - only modified files are written
- **Unknown element preservation** - forward compatibility

### Validation (`TALXIS.Platform.Metadata.Validation`)
- **XSD schema validation** - 23 component schemas
- **Structural rules** - duplicate GUIDs, missing references, naming conventions
- **Constraint checking** - attribute limits, option set value ranges

## Design Principles

1. **Roundtrip safe** - the model preserves everything it reads, even elements it doesn't understand
2. **Layered** - components know about solution layers, not just the active (merged) state
3. **Validating** - constraints are enforced in-memory, not just at serialization time
4. **No runtime dependencies** - pure model library, no Dataverse SDK, no HTTP, no SQL
5. **netstandard2.0** - maximum compatibility (MSBuild tasks, scripts, CLI, runtime services)

## Status

🚧 **Design phase** - see [docs/](docs/) for architecture documents and roadmap.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
