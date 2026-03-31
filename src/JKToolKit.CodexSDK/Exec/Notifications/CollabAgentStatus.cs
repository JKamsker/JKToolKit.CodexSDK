namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents an agent lifecycle status reported by collab events.
/// </summary>
public enum CollabAgentStatus
{
    /// <summary>
    /// The status is missing or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The agent is waiting for initialization.
    /// </summary>
    PendingInit = 1,

    /// <summary>
    /// The agent is running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The agent was interrupted and may later continue.
    /// </summary>
    Interrupted = 3,

    /// <summary>
    /// The agent completed successfully.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The agent errored.
    /// </summary>
    Errored = 5,

    /// <summary>
    /// The agent was shut down.
    /// </summary>
    Shutdown = 6,

    /// <summary>
    /// The agent was not found.
    /// </summary>
    NotFound = 7
}
