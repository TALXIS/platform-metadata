# Contributing

## Development Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building and running tests)
- Git

### Build
```sh
dotnet build
```

### Test
```sh
dotnet test
```

## Project Structure

```
src/
  TALXIS.Platform.Metadata/           — Core metadata model (netstandard2.0)
docs/
  architecture.md                     — Design and namespace structure
  roadmap.md                          — Phased implementation plan
tests/
  TALXIS.Platform.Metadata.Tests/     — Unit and roundtrip tests
```

## Design Guidelines

### Roundtrip safety is non-negotiable
Every serializer must pass: `Load(file) → Save(file) = zero byte diff`. Unknown XML elements and attributes must be preserved, not dropped.

### No external dependencies
The metadata model must depend only on `System.*` namespaces. No Dataverse SDK, no HTTP clients, no database drivers. Consumers bring their own I/O.

### netstandard2.0
The library must target netstandard2.0 for maximum consumer compatibility (MSBuild tasks, template scripts, CLI, runtime services).

### Component types are extensible
New component types should be addable without modifying existing code. Use `ComponentDefinitionRegistry` to register type-specific behavior.
