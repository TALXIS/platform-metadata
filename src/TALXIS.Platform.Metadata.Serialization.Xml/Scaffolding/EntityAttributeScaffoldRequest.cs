namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Applies a rendered pp-entity-attribute scaffold to an unpacked solution.
/// </summary>
public sealed class EntityAttributeScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the entity that receives the attribute (e.g. udpp_warehouseitem).
    /// </summary>
    public string EntitySchemaName { get; set; } = "";

    /// <summary>
    /// Rendered <c>&lt;attribute&gt;</c> XML file produced by the template.
    /// </summary>
    public string AttributeFilePath { get; set; } = "";

    /// <summary>
    /// Option spec for choice columns: comma-separated labels or Label:Value pairs.
    /// </summary>
    public string? OptionSetOptions { get; set; }

    /// <summary>
    /// Rendered global option set file (OptionSets/&lt;name&gt;.xml). Options go here instead of the attribute when set.
    /// </summary>
    public string? GlobalOptionSetFilePath { get; set; }

    /// <summary>
    /// Schema name of the global option set to register as a RootComponent (type 9).
    /// </summary>
    public string? GlobalOptionSetSchemaName { get; set; }

    /// <summary>
    /// Rendered money base attribute file. Enables the money support step.
    /// </summary>
    public string? MoneyBaseAttributeFilePath { get; set; }

    /// <summary>
    /// Rendered transactioncurrencyid attribute file (applied only when the entity has full metadata).
    /// </summary>
    public string? CurrencyAttributeFilePath { get; set; }

    /// <summary>
    /// Rendered exchangerate attribute file (applied only when the entity has full metadata).
    /// </summary>
    public string? ExchangeRateAttributeFilePath { get; set; }

    /// <summary>
    /// Rendered <c>&lt;EntityRelationship&gt;</c> XML file. Enables the lookup relationship step.
    /// </summary>
    public string? LookupRelationshipFilePath { get; set; }

    /// <summary>
    /// Name of the lookup relationship (e.g. udpp_account_udpp_warehouseitem_supplier).
    /// </summary>
    public string? LookupRelationshipName { get; set; }

    /// <summary>
    /// Logical name of the entity the lookup points to (e.g. account).
    /// </summary>
    public string? ReferencedEntityName { get; set; }
}
