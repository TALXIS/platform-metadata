# PAC CLI ModelBuilder — Decompilation Analysis

Reference material from decompiled `microsoft.powerapps.cli.tool.2.6.3`.

## Attribute Type → C# Type Mapping (canonical)

| AttributeTypeCode | C# Type | Notes |
|---|---|---|
| Boolean | `bool?` | |
| DateTime | `DateTime?` | |
| Decimal | `decimal?` | |
| Double | `double?` | |
| Integer | `int?` | |
| BigInt | `long?` | |
| Uniqueidentifier | `Guid?` | |
| String / Memo / EntityName | `string` | ref type |
| Money | `Money` | SDK type |
| Customer / Lookup / Owner | `EntityReference` | SDK type |
| ManagedProperty | `BooleanManagedProperty` | SDK type |
| CalendarRules | `object` | |
| Picklist / Status / State (with enum) | `Nullable<GeneratedEnum>` | |
| Picklist / Status (no enum) | `OptionSetValue?` | |
| MultiSelectPicklist | `IEnumerable<GeneratedEnum>` | |
| PartyList | `IEnumerable<ActivityParty>` | |
| Image | `byte[]` | |
| File | `Guid?` | file column ID |

## Code Generation Architecture

- Engine: **System.CodeDom** (legacy — builds AST, renders to C#/VB)
- Output: split files — `Entities/`, `Messages/`, `OptionSets/`, `ServiceContext.cs`
- No TypeScript support

## Key Interfaces

| Interface | Maps to our package |
|---|---|
| `IOrganizationMetadata` | `TALXIS.Platform.Metadata` (core) |
| `IMetadataProviderService` | `.Provider.Dataverse` |
| `ICodeGenerationService` | `.CodeGen.CSharp` |
| `INamingService` | `.CodeGen.*` (shared) |
| `ITypeMappingService` | Core (universal type mapping) |
| `ICodeWriterFilterService` | `.CodeGen.*` (filtering) |

## Metadata Retrieval

- `RetrieveAllEntitiesRequest` or `RetrieveMetadataChangesRequest` (batched, max 20 per request)
- SDK messages via FetchXML with paging
- Global option sets via `RetrieveAllOptionSets`
- Always live connection — no file-based caching
