namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Dispatches a rendered component scaffold to the applier registered for its component type.
/// Single entry point for all component templates: migrating another template means registering
/// an applier for its type, not adding a new CLI command. Type names resolve through
/// <see cref="ComponentDefinitionRegistry"/>, so registry aliases (e.g. <c>Column</c>) work too.
/// Appliers are transitional adapters for the template migration; they move to the typed
/// workspace model once the manipulation API (roadmap milestone 4) lands.
/// </summary>
public static class ComponentScaffold
{
    private static readonly Dictionary<string, Func<ComponentScaffoldRequest, ScaffoldResult>> Appliers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Attribute"] = ApplyAttribute,
            ["FormRow"] = ApplyFormRow,
            ["FormCell"] = ApplyFormCell,
            ["FormControl"] = ApplyFormControl,
            ["FormColumn"] = ApplyFormColumn,
            ["FormSection"] = ApplyFormSection,
            ["FormTab"] = ApplyFormTab,
            ["FormDialogTabFooter"] = ApplyFormDialogTabFooter,
            ["FormParameter"] = ApplyFormParameter,
            ["FormEventHandler"] = ApplyFormEventHandler,
            ["ControlParameter"] = ApplyControlParameter,
            ["Role"] = ApplySecurityRole,
            ["Workflow"] = ApplyFlow,
            ["OptionSet"] = ApplyOptionSetGlobal,
            ["SystemForm"] = ApplyEntityForm,
            ["EnvironmentVariableDefinition"] = ApplyEnvironmentVariableDefinition,
        };

    public static IReadOnlyCollection<string> SupportedComponentTypes => Appliers.Keys;

    /// <summary>
    /// Registers (or replaces) the scaffold applier for a component type.
    /// </summary>
    public static void Register(string componentType, Func<ComponentScaffoldRequest, ScaffoldResult> applier) =>
        Appliers[Canonicalize(componentType)] = applier;

    public static ScaffoldResult Apply(ComponentScaffoldRequest request)
    {
        if (!Appliers.TryGetValue(Canonicalize(request.ComponentType), out var applier))
        {
            throw new NotSupportedException(
                $"Component type '{request.ComponentType}' has no scaffold applier. Supported types: {string.Join(", ", Appliers.Keys)}.");
        }
        // Uses the supplied path only when it identifies a solution root; otherwise
        // auto-detects the root from the current directory.
        request.SolutionRootPath = SolutionRootLocator.Resolve(request.SolutionRootPath, Directory.GetCurrentDirectory());
        return applier(request);
    }

    // The component-definition registry is the single component-name vocabulary; names it does
    // not know pass through unchanged so an applier can still be registered under a custom key.
    private static string Canonicalize(string componentType) =>
        ComponentDefinitionRegistry.GetByName(componentType)?.Name ?? componentType;

    // Files: attribute (required), money-base, currency, exchange-rate, relationship, global-optionset.
    // Parameters: entity (required), options, global-optionset-name, relationship-name, referenced-entity.
    private static ScaffoldResult ApplyAttribute(ComponentScaffoldRequest request) =>
        EntityAttributeScaffold.Apply(new EntityAttributeScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = RequiredParameter(request, "entity"),
            AttributeFilePath = RequiredFile(request, "attribute"),
            OptionSetOptions = Optional(request.Parameters, "options"),
            GlobalOptionSetFilePath = Optional(request.Files, "global-optionset"),
            GlobalOptionSetSchemaName = Optional(request.Parameters, "global-optionset-name"),
            MoneyBaseAttributeFilePath = Optional(request.Files, "money-base"),
            CurrencyAttributeFilePath = Optional(request.Files, "currency"),
            ExchangeRateAttributeFilePath = Optional(request.Files, "exchange-rate"),
            LookupRelationshipFilePath = Optional(request.Files, "relationship"),
            LookupRelationshipName = Optional(request.Parameters, "relationship-name"),
            ReferencedEntityName = Optional(request.Parameters, "referenced-entity"),
        });

    // Files: row (required). Parameters: entity, form-type, form-id and the placement set, all optional.
    private static ScaffoldResult ApplyFormRow(ComponentScaffoldRequest request) =>
        FormRowScaffold.Apply(new FormRowScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            RowFilePath = RequiredFile(request, "row"),
        });

    // Files: cell (required), dialog-cell. Parameters: entity, form-type, form-id and the placement set, all optional.
    private static ScaffoldResult ApplyFormCell(ComponentScaffoldRequest request) =>
        FormCellScaffold.Apply(new FormCellScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            CellFilePath = RequiredFile(request, "cell"),
            DialogCellFilePath = Optional(request.Files, "dialog-cell"),
        });

    // Files: control (required), dialog-control. Parameters: entity, form-type, form-id, the placement set,
    // plus row-span/col-span passed only for SubGrid controls.
    private static ScaffoldResult ApplyFormControl(ComponentScaffoldRequest request) =>
        FormControlScaffold.Apply(new FormControlScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            ControlFilePath = RequiredFile(request, "control"),
            DialogControlFilePath = Optional(request.Files, "dialog-control"),
            RowSpan = OptionalKnown(request.Parameters, "row-span"),
            ColumnSpan = OptionalKnown(request.Parameters, "col-span"),
        });

    // Files: column (required). Parameters: entity, form-type, form-id, tab targeting, all optional.
    private static ScaffoldResult ApplyFormColumn(ComponentScaffoldRequest request) =>
        FormColumnScaffold.Apply(new FormColumnScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            ColumnFilePath = RequiredFile(request, "column"),
        });

    // Files: section (required). Parameters: section-name (required); entity, form-type,
    // form-id, section-id and the tab/column targeting, all optional.
    private static ScaffoldResult ApplyFormSection(ComponentScaffoldRequest request) =>
        FormSectionScaffold.Apply(new FormSectionScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            SectionId = OptionalKnown(request.Parameters, "section-id"),
            SectionName = RequiredParameter(request, "section-name"),
            SectionFilePath = RequiredFile(request, "section"),
        });

    // Files: tab (required). Parameters: display-name (required); entity, form-type,
    // form-id, tab-id and remove-default-tab, all optional.
    private static ScaffoldResult ApplyFormTab(ComponentScaffoldRequest request) =>
        FormTabScaffold.Apply(new FormTabScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            TabId = OptionalId(request.Parameters, "tab-id"),
            DisplayName = RequiredParameter(request, "display-name"),
            RemoveDefaultTab = string.Equals(Optional(request.Parameters, "remove-default-tab"), "True", StringComparison.OrdinalIgnoreCase),
            TabFilePath = RequiredFile(request, "tab"),
        });

    // Parameters: form-id, tab-id, tab-index and tab-footer-id, all optional.
    private static ScaffoldResult ApplyFormDialogTabFooter(ComponentScaffoldRequest request) =>
        FormDialogTabFooterScaffold.Apply(new FormDialogTabFooterScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            TabFooterId = OptionalId(request.Parameters, "tab-footer-id"),
        });

    // Parameters: parameter-name, parameter-type (required); entity, form-type, form-id optional.
    private static ScaffoldResult ApplyFormParameter(ComponentScaffoldRequest request) =>
        FormParameterScaffold.Apply(new FormParameterScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            ParameterName = RequiredParameter(request, "parameter-name"),
            ParameterType = RequiredParameter(request, "parameter-type"),
        });

    // Parameters: library-name, event-name, function-name (required); entity, form-type, form-id,
    // attribute-name, library-unique-id, handler-unique-id and pass-execution-context optional.
    private static ScaffoldResult ApplyFormEventHandler(ComponentScaffoldRequest request) =>
        FormEventHandlerScaffold.Apply(new FormEventHandlerScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntityLogicalName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            LibraryName = RequiredParameter(request, "library-name"),
            LibraryUniqueId = OptionalId(request.Parameters, "library-unique-id"),
            EventName = RequiredParameter(request, "event-name"),
            AttributeName = OptionalKnown(request.Parameters, "attribute-name"),
            FunctionName = RequiredParameter(request, "function-name"),
            HandlerUniqueId = OptionalId(request.Parameters, "handler-unique-id"),
            PassExecutionContext = Optional(request.Parameters, "pass-execution-context") ?? "true",
        });

    // Files: parameters (required). Parameters: entity, form-type, form-id and the placement set, all optional.
    private static ScaffoldResult ApplyControlParameter(ComponentScaffoldRequest request) =>
        ControlParameterScaffold.Apply(new ControlParameterScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
            FormType = OptionalKnown(request.Parameters, "form-type"),
            FormId = OptionalId(request.Parameters, "form-id"),
            Placement = PlacementFrom(request),
            ParametersFilePath = RequiredFile(request, "parameters"),
        });

    // Parameters: role-id (required).
    private static ScaffoldResult ApplySecurityRole(ComponentScaffoldRequest request) =>
        SecurityRoleScaffold.Apply(new SecurityRoleScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            RoleId = RequiredParameter(request, "role-id").Trim('{', '}'),
        });

    // Parameters: workflow-id (required).
    private static ScaffoldResult ApplyFlow(ComponentScaffoldRequest request) =>
        FlowScaffold.Apply(new FlowScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            WorkflowId = RequiredParameter(request, "workflow-id").Trim('{', '}'),
        });

    // Parameters: optionset-name, options (both required).
    private static ScaffoldResult ApplyOptionSetGlobal(ComponentScaffoldRequest request) =>
        OptionSetGlobalScaffold.Apply(new OptionSetGlobalScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            OptionSetName = RequiredParameter(request, "optionset-name"),
            Options = RequiredParameter(request, "options"),
        });

    // Files: definition (required). Parameters: schema-name (required).
    private static ScaffoldResult ApplyEnvironmentVariableDefinition(ComponentScaffoldRequest request) =>
        EnvironmentVariableDefinitionScaffold.Apply(new EnvironmentVariableDefinitionScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            DefinitionFilePath = RequiredFile(request, "definition"),
            SchemaName = RequiredParameter(request, "schema-name"),
        });

    // Files: form (required). Parameters: form-type (required); form-id,
    // form-name, dialog-unique-name and entity optional.
    private static ScaffoldResult ApplyEntityForm(ComponentScaffoldRequest request) =>
        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = request.SolutionRootPath,
            FormType = RequiredParameter(request, "form-type"),
            FormFilePath = RequiredFile(request, "form"),
            FormId = OptionalId(request.Parameters, "form-id"),
            FormName = Optional(request.Parameters, "form-name"),
            DialogUniqueName = OptionalKnown(request.Parameters, "dialog-unique-name"),
            EntitySchemaName = OptionalKnown(request.Parameters, "entity"),
        });

    private static FormPlacement PlacementFrom(ComponentScaffoldRequest request) => new()
    {
        TabId = OptionalId(request.Parameters, "tab-id"),
        TabIndex = OptionalKnown(request.Parameters, "tab-index"),
        ColumnIndex = OptionalKnown(request.Parameters, "column-index"),
        SectionId = OptionalId(request.Parameters, "section-id"),
        SectionIndex = OptionalKnown(request.Parameters, "section-index"),
        RowIndex = OptionalKnown(request.Parameters, "row-index"),
        SetToTabFooter = string.Equals(Optional(request.Parameters, "set-to-tab-footer"), "True", StringComparison.OrdinalIgnoreCase),
    };

    private static string? OptionalId(IReadOnlyDictionary<string, string> map, string name) =>
        OptionalKnown(map, name)?.Trim('{', '}');

    // Omitted form parameters are represented by "unknown", "unknownFormId" or "unknownTabId".
    private static string? OptionalKnown(IReadOnlyDictionary<string, string> map, string name)
    {
        var value = Optional(map, name);
        return string.IsNullOrEmpty(value) || value == "unknown" || value == "unknownFormId" || value == "unknownTabId" ? null : value;
    }

    private static string RequiredParameter(ComponentScaffoldRequest request, string name) =>
        Optional(request.Parameters, name)
            ?? throw new ArgumentException($"Missing required parameter '{name}' for component type '{request.ComponentType}'.");

    private static string RequiredFile(ComponentScaffoldRequest request, string name) =>
        Optional(request.Files, name)
            ?? throw new ArgumentException($"Missing required file '{name}' for component type '{request.ComponentType}'.");

    private static string? Optional(IReadOnlyDictionary<string, string> map, string name) =>
        map.TryGetValue(name, out var value) ? value : null;
}
