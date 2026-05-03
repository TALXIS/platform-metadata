namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Dataverse component state values used by solution layers.
/// </summary>
public enum ComponentState
{
    /// <summary>
    /// Published component state.
    /// </summary>
    Publish = 0,

    /// <summary>
    /// Unpublished component state.
    /// </summary>
    Unpublish = 1,

    /// <summary>
    /// Deleted component state.
    /// </summary>
    Delete = 2,

    /// <summary>
    /// Deleted before publication.
    /// </summary>
    UnpublishedDelete = 3,

    /// <summary>
    /// Snapshot component state used by Dataverse internals.
    /// </summary>
    Snapshot = 4,

    /// <summary>
    /// Staged component state used by Dataverse upgrade flows.
    /// </summary>
    Stage = 5
}
