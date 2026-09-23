namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// What every typed solution component knows about itself: its stable identity and the source document it owns.
/// </summary>
public interface ISolutionComponent
{
    /// <summary>
    /// Stable identity (type + object id) derived from the component's own key fields.
    /// </summary>
    ComponentIdentity Identity { get; }

    /// <summary>
    /// Logical key of the source document that owns this component inside a workspace.
    /// </summary>
    string DocumentKey { get; }
}
