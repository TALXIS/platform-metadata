namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Dispatches a rendered component scaffold to the applier registered for its component type.
/// Single entry point for all component templates: migrating another template means adding
/// an applier here, not a new CLI command.
/// </summary>
public static class ComponentScaffold
{
    private static readonly Dictionary<string, Func<ComponentScaffoldRequest, ScaffoldResult>> Appliers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Attribute"] = ApplyAttribute,
        };

    public static IReadOnlyCollection<string> SupportedComponentTypes => Appliers.Keys;

    public static ScaffoldResult Apply(ComponentScaffoldRequest request)
    {
        if (!Appliers.TryGetValue(request.ComponentType, out var applier))
        {
            throw new NotSupportedException(
                $"Component type '{request.ComponentType}' has no scaffold applier. Supported types: {string.Join(", ", Appliers.Keys)}.");
        }
        return applier(request);
    }

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

    private static string RequiredParameter(ComponentScaffoldRequest request, string name) =>
        Optional(request.Parameters, name)
            ?? throw new ArgumentException($"Missing required parameter '{name}' for component type '{request.ComponentType}'.");

    private static string RequiredFile(ComponentScaffoldRequest request, string name) =>
        Optional(request.Files, name)
            ?? throw new ArgumentException($"Missing required file '{name}' for component type '{request.ComponentType}'.");

    private static string? Optional(IReadOnlyDictionary<string, string> map, string name) =>
        map.TryGetValue(name, out var value) ? value : null;
}
