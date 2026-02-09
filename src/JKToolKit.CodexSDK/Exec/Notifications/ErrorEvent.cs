namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents an error emitted by Codex during a session.
/// </summary>
public sealed record ErrorEvent : CodexEvent
{
    /// <summary>
    /// Gets the error message emitted by Codex.
    /// </summary>
    public required string Message { get; init; }
}

