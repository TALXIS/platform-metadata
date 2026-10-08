namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Where a column's value comes from (<c>SourceType</c> in Entity.xml): stored, calculated, rollup or Power Fx formula.
/// </summary>
public enum AttributeSourceType
{
    Simple = 0,
    Calculated = 1,
    Rollup = 2,
    Formula = 3
}
