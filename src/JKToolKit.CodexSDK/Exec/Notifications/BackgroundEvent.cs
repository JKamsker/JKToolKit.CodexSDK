namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a background status/update message from Codex.
/// </summary>
public sealed record BackgroundEvent : CodexEvent
{
    /// <summary>
    /// Gets the background message text emitted by Codex.
    /// </summary>
    public required string Message { get; init; }
}

