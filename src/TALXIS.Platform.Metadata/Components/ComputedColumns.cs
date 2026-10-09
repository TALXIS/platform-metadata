namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Columns whose values Dataverse computes itself, whatever their create and update flags say.
/// </summary>
public static class ComputedColumns
{
    /// <summary>
    /// Whether Dataverse computes <paramref name="attribute"/>: a calculated, rollup or formula column, or the base-currency copy of a money column.
    /// </summary>
    public static bool Contains(AttributeMetadata attribute) =>
        attribute.SourceType is AttributeSourceType.Calculated or AttributeSourceType.Rollup or AttributeSourceType.Formula
        || (attribute.AttributeType == AttributeType.Money && attribute.LogicalName.EndsWith("_base", StringComparison.OrdinalIgnoreCase));
}
