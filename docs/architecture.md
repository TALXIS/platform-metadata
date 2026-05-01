# Architecture

## Overview

TALXIS Platform Metadata is a typed C# object model for model-driven platform components. It serves as the shared kernel for all tools and services that need to understand, validate, or manipulate platform metadata — whether from files on disk, a live environment API, or an in-memory workspace.

## Layered Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        Consumers                              │
│  CLI  │  Build SDK  │  Templates  │  Language Server  │  EDS  │
├──────────────────────────────────────────────────────────────┤
│                    Workspace Context                          │
│  IWorkspaceContext (file system, transactional, in-memory)    │
├──────────────────────────────────────────────────────────────┤
│                     Serialization                             │
│  SolutionPackager XML ↔ Model   │   API ↔ Model              │
│  Roundtrip-safe  │  Minimal diff  │  Unknown preservation     │
├──────────────────────────────────────────────────────────────┤
│                     Validation                                │
│  XSD schemas  │  Structural rules  │  Constraint checking     │
├──────────────────────────────────────────────────────────────┤
│                    Metadata Model                             │
│  Entity  │  Attribute  │  Relationship  │  OptionSet          │
│  Form  │  View  │  PluginAssembly  │  SecurityRole  │  ...    │
├──────────────────────────────────────────────────────────────┤
│                   Solution Model                              │
│  Solution  │  Publisher  │  ComponentDefinition                │
│  Layer stack  │  State machine  │  Merge behavior              │
└──────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### 1. Components as objects with state

Each metadata component is a C# class, not a raw XML node. Components know their type, their identity, their constraints, and their serialization format.

```csharp
var entity = new EntityMetadata("udpp_warehouse")
{
    DisplayName = new Label("Warehouse", 1033),
    PluralName = new Label("Warehouses", 1033),
    Ownership = OwnershipType.UserOwned,
};

entity.AddAttribute(new StringAttributeMetadata("udpp_name")
{
    MaxLength = 200,
    IsPrimaryName = true,
});
```

### 2. Solution layers modeled explicitly

Components don't just have a single state — they have a stack of layers representing different solutions. The active (visible) state is computed by resolving the layer stack, matching how the platform resolves layers at runtime.

```csharp
var component = workspace.GetComponent(ComponentType.Entity, "udpp_warehouse");
var layers = component.Layers; // [Base, ManagedSolution1, ActiveCustomization]
var active = component.ActiveLayer; // the resolved/merged state
```

### 3. Roundtrip-safe serialization

The model preserves XML elements and attributes it doesn't understand. This ensures:
- `Load("Entity.xml") → Save("Entity.xml")` produces zero git diff
- Forward compatibility with newer platform versions
- Unknown customizations are not silently dropped

Implementation: each model class carries an `XElement _source` that holds the original XML. Known properties read from / write to this source. Unknown children are preserved as-is.

### 4. Component definitions drive behavior

Each component type has a `ComponentDefinition` that declares:
- How it serializes (XML element name, identity attribute, file layout)
- Whether it's mergeable (forms) or replace-on-import (entities)
- Dependency rules (what it depends on, what depends on it)
- Validation rules (required fields, naming constraints)

This mirrors the internal Dataverse `IComponentDefinition` architecture.

### 5. Workspace context abstraction

The model doesn't touch the filesystem directly. All I/O goes through `IWorkspaceContext`, which can be:
- `FileSystemContext` — direct disk access (standalone scripts, `dotnet new`)
- `TransactionalContext` — buffered writes with rollback (CLI)
- `InMemoryContext` — no disk at all (language server, tests)
- `ApiContext` — reads from live environment API (future)

## Namespace Structure

```
TALXIS.Platform.Metadata
├── EntityMetadata, AttributeMetadata, RelationshipMetadata, ...
├── OptionSetMetadata, FormMetadata, ViewMetadata, ...
├── Label, LocalizedLabel
├── ComponentType (enum)
└── OwnershipType, AttributeType, ... (enums)

TALXIS.Platform.Metadata.Solutions
├── Solution, Publisher
├── SolutionComponent, ComponentLayer
├── ComponentDefinition, ComponentDefinitionRegistry
└── ComponentState, ComponentOperation (enums)

TALXIS.Platform.Metadata.Serialization
├── SolutionPackagerReader  — disk → model
├── SolutionPackagerWriter  — model → disk (roundtrip-safe)
├── SolutionPackagerLayout  — well-known paths and conventions
└── XmlPreservingSerializer — base class for roundtrip serialization

TALXIS.Platform.Metadata.Validation
├── SchemaValidator         — XSD-based validation
├── StructuralValidator     — cross-file consistency checks
├── NamingValidator         — naming convention enforcement
└── Schemas/                — embedded XSD resources

TALXIS.Platform.Metadata.Workspace
├── IWorkspaceContext       — file I/O abstraction
├── FileSystemContext       — direct disk access
├── TransactionalContext    — buffered with rollback
└── InMemoryContext         — for tests and language server
```

## Target Framework

`netstandard2.0` — required for:
- MSBuild tasks (run in MSBuild's host process)
- Template post-action scripts (.NET 10 file-based apps can consume netstandard2.0)
- CLI (.NET 10)
- Language server (.NET 10)
- Potential Mono/Unity scenarios

Zero external dependencies beyond `System.Xml.Linq` and `System.Text.Json`.
