namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a top-level task.
/// </summary>
public sealed record TaskStartedEvent : CodexEvent
{
    /// <summary>
    /// Gets the model context window size when provided by Codex.
    /// </summary>
    public int? ModelContextWindow { get; init; }
}

