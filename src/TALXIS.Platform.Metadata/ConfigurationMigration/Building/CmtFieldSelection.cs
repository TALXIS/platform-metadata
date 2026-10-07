namespace TALXIS.Platform.Metadata.ConfigurationMigration.Building;

/// <summary>
/// How many columns <see cref="CmtSchemaBuilder.BuildEntity"/> puts into a generated schema entity.
/// </summary>
public enum CmtFieldSelection
{
    /// <summary>
    /// Primary id and name, custom columns and required non-system columns, plus transactioncurrencyid when a money column is selected.
    /// </summary>
    Minimal,

    /// <summary>
    /// Minimal plus every lookup (transactioncurrencyid included), customer and choice column, and overriddencreatedon.
    /// </summary>
    Standard,

    /// <summary>
    /// Every column that can be both created and updated, plus overriddencreatedon, createdby and modifiedby.
    /// </summary>
    Full
}
