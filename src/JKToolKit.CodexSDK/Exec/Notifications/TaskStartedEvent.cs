namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a top-level task.
/// </summary>
public sealed record TaskStartedEvent : CodexEvent
{
    /// <summary>
    /// Gets the turn id for the started task, when provided.
    /// </summary>
    public string? TurnId { get; init; }

    /// <summary>
    /// Gets the model context window size when provided by Codex.
    /// </summary>
    public int? ModelContextWindow { get; init; }
}

