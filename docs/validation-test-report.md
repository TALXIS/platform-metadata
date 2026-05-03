# Validation Test Report

Tested TALXIS.Platform.Metadata validation (WorkspaceValidator) against 4 repositories with real Dataverse-exported solutions.

## Summary

| Repository | Solutions | XML Files | Errors | Model Load | Result |
|------------|-----------|-----------|--------|------------|--------|
| conference-session | 4 | 49 | 0 | All OK | PASS |
| PCT23005 | 5 | 461 | 0 | All OK | PASS |
| PCT20004 | 35 | 2,000 | 0 | All OK | PASS |
| INT0006 (sample 10) | 10 | ~1,500 | 12 | All OK | 1 solution has duplicate GUIDs |

**Total: 54 solutions tested, 0 schema errors, 12 GUID duplicates (1 solution), all models loaded successfully.**

## Detailed Results

### conference-session (4 solutions, our scaffolded project)

| Solution | Errors | Components Loaded |
|----------|--------|-------------------|
| Solutions.DataModel | 0 | 2 entities (41 attrs), 6 forms, 12 views, 1 option set, 1 relationship |
| Solutions.Logic | 0 | 1 plugin assembly, 2 steps |
| Solutions.Security | 0 | 2 roles |
| Solutions.UI | 0 | 2 entities (0 attrs), 2 forms, 1 app module, 1 sitemap |

### PCT23005 (5 solutions, customer project)

| Solution | Errors | Components Loaded |
|----------|--------|-------------------|
| Apps.Home | 0 | 1 entity, 35 forms, 70 views, 4 sitemaps, 20 web resources, 5 generic |
| Configuration | 0 | 5 roles, 25 generic |
| Features.Shared.Composition | 0 | 1 plugin assembly, 10 steps, 46 workflows, 6 generic |
| Features.Vouchers.Composition | 0 | 1 plugin assembly, 4 steps, 4 workflows |
| Model | 0 | 19 entities (362 attrs), 33 forms, 74 views, 7 option sets, 49 relationships, 2 web resources |

### PCT20004 (35 solutions, largest customer project)

All 35 solutions pass with 0 errors. Highlights:

| Solution | Components |
|----------|------------|
| AccountsReceivable/Model | 26 entities (542 attrs), 66 forms, 125 views, 13 option sets, 62 rels, 8 roles, 17 web resources |
| VAT/Model | 16 entities (648 attrs), 48 forms, 96 views, 16 option sets, 30 rels, 11 web resources |
| Foodsafety/Model | 18 entities (374 attrs), 33 forms, 69 views, 12 option sets, 52 rels, 2 roles |
| VAT/Features.Rules.Composition | 156 generic components (business rules) |
| Foodsafety/Features.Automation.Composition | 64 workflows (cloud flows) |
| AccountsReceivable/Features.Shared.Composition | 55 workflows |

### INT0006 (10 solutions sampled from 186, large ISV monorepo)

| Solution | Errors | Components |
|----------|--------|------------|
| Buildings/Accounting/Shared.Model.DataLayer | **12** | 2 entities (51 attrs), 12 forms, 12 views, 1 option set, 18 rels |
| Buildings/Sales/Apps.Start.PresentationLayer | 0 | 34 forms, 64 views, 1 sitemap, 16 web resources, 5 workflows |
| Areas/Environment/Bootstrap/Model | 0 | 63 entities (1294 attrs), 156 forms, 308 views, 28 option sets, 93 rels |
| Areas/Verticals/Automotive/Model | 0 | 8 entities (171 attrs), 18 forms, 36 views, 5 option sets, 16 rels |
| (6 others) | 0 | Various |

The 12 errors in Accounting are **duplicate GUID** errors: managed/unmanaged form pairs for `talxis_Wallet` entity share the same `formid`. This is a real data issue in the repo.

## XSD Coverage Map

| Component Type | Has XSD | Schema warnings | Notes |
|----------------|---------|-----------------|-------|
| Entity | YES | Many | XSD covers core structure, warns on newer attrs |
| Form | YES | Some | 1118-line XSD, good coverage |
| SavedQuery (view) | YES | Some | Covers structure well |
| OptionSet | YES | Few | Good coverage |
| Role | YES | Few | Good coverage |
| PluginAssembly | YES | Some | Basic coverage |
| SdkMessageProcessingStep | YES | Some | Fixed in milestone 2 |
| AppModule | YES | Some | Fixed in milestone 2 |
| SiteMap | YES | Some | Good coverage |
| WebResource | YES | Few | Simple structure |
| Workflow | YES | Some | .data.xml metadata only |
| Relationship | YES | Few | Basic structure |
| Solution.xml | YES | Few | Good coverage |
| Customizations.xml | YES | Few | Residual format supported |
| Ribbon | YES | Few | RibbonDiff format |
| FieldSecurityProfile | YES | Few | |
| ConnectionRoles | YES | Few | |
| EnvironmentVariable | YES | Few | |
| Dialog | YES | Few | |
| Fetch | YES | Few | FetchXml subschema |
| Cloud Flows (.json) | NO | N/A | JSON files, no JSON schema defined for flows |
| Canvas Apps | NO | N/A | .msapp binary format |
| Business Rules (generic) | NO | N/A | Loaded as generic components |
| Environment Variable values | NO | N/A | Loaded as generic components |

## Reader Coverage Map

| Component Type | Reader Support | Notes |
|----------------|---------------|-------|
| Entity + Attributes | Full | All attribute subtypes parsed |
| Forms | Full | Identity + metadata, body preserved as raw |
| Views (SavedQuery) | Full | FetchXml + LayoutXml as strings |
| OptionSets | Full | Global + options |
| Relationships | Full | 1:N and N:N |
| PluginAssemblies | Full | With PluginTypes children |
| SdkMessageProcessingSteps | Full | With StepImages children |
| SecurityRoles | Full | With privileges |
| AppModules | Full | With components + role maps |
| SiteMaps | Full | Identity + settings |
| WebResources | Full | .data.xml metadata |
| Workflows | Full | .data.xml metadata |
| Generic Components | Pass-through | Raw XML preserved, ID/name extracted heuristically |
| Cloud Flows | Via Workflows | .data.xml is Workflow format |
| Environment Variables | Via Generic | Loaded as generic components |
| Business Rules | Via Generic | Loaded as generic components |
| Canvas Apps | Not loaded | .msapp binary not scanned |
| Translations CSV | Not loaded | Non-XML format |

## Warnings Analysis

All warnings across all repos are of the form: "Could not find schema information for the element/attribute 'X'". These are XSD informational warnings, not errors. They occur because:

1. Dataverse XML uses elements/attributes not in our XSDs (newer platform features)
2. Some XML files (like EntityRelationships at root level) don't match any XSD
3. Custom publisher-prefixed elements aren't in schemas

Warning counts scale linearly with solution size (more entities/attributes = more warnings).

## Error Injection Tests

Copied conference-session to temp, injected 7 errors, validated:

| # | Injection | Component | Caught? | Error Message |
|---|-----------|-----------|---------|---------------|
| 1 | Invalid `<FakeElement>` in Entity.xml | Entity | CAUGHT | Invalid child element 'FakeElement' |
| 2 | Malformed XML (`<broken`) in view | SavedQuery | CAUGHT | Unexpected end of file + model load failure |
| 3 | Empty file (0 bytes) in form | Form | CAUGHT | Root element is missing + model load failure |
| 4 | `value="notanumber"` in OptionSet | OptionSet | CAUGHT | 'notanumber' is not a valid Integer value |
| 5 | `<BadChild/>` in RolePrivileges | Role | CAUGHT | Invalid child element 'BadChild' |
| 6 | Duplicate GUID across step files | Step | MISSED | GuidValidator on master doesn't scan attributes (fixed in PR #22) |
| 7 | `<Broken/>` in AppModule | AppModule | CAUGHT | Invalid child element 'Broken' |

**6/7 caught.** Test 6 confirms PR #22 (attribute GUID scanning) is needed.

## Recommendations

### Immediate (before next release)
1. Merge PR #22 (GuidValidator attribute scanning) to fix the missed duplicate GUID test
2. Consider downgrading schema-missing warnings to a lower severity or filtering them (they dominate output)
3. The INT0006 duplicate GUID finding is a real bug in that repo that should be reported to the team

### Future improvements (file as issues)
4. Update CLI and Build SDK to use WorkspaceValidator instead of manual wiring
5. Add JSON schema for cloud flow .json files
6. Add cross-reference validation (form references entity, step references plugin)
7. Consider suppressing schema-missing warnings for known safe patterns
8. Add environment variable definition/value structural validation
