namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Indicates that earlier conversation context was compacted.
/// </summary>
public sealed record CompactionCheckpointWarningEvent : CodexEvent
{
    /// <summary>
    /// Gets the warning message text emitted by Codex.
    /// </summary>
    public required string Message { get; init; }
}

