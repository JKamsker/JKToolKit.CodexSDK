using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the receiver status reported for a collab tool call.
/// </summary>
[JsonConverter(typeof(CollabReceiverStatusJsonConverter))]
public enum CollabReceiverStatus
{
    /// <summary>
    /// The status is missing or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The receiver has been requested, but is not yet running.
    /// </summary>
    PendingInit = 1,

    /// <summary>
    /// The receiver is running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The receiver was interrupted and may later continue.
    /// </summary>
    Interrupted = 3,

    /// <summary>
    /// The receiver completed successfully.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The receiver errored.
    /// </summary>
    Errored = 5,

    /// <summary>
    /// The receiver was shut down.
    /// </summary>
    Shutdown = 6,

    /// <summary>
    /// The receiver was not found.
    /// </summary>
    NotFound = 7
}
