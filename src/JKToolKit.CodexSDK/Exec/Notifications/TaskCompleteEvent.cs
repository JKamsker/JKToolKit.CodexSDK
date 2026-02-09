namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a top-level task.
/// </summary>
public sealed record TaskCompleteEvent : CodexEvent
{
    /// <summary>
    /// Gets the last agent message for the completed task, when provided.
    /// </summary>
    public string? LastAgentMessage { get; init; }
}

