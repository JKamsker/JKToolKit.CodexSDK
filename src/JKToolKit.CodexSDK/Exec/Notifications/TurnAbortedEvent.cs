namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents an aborted turn (e.g. interrupted).
/// </summary>
public sealed record TurnAbortedEvent : CodexEvent
{
    /// <summary>
    /// Gets the reason the turn was aborted.
    /// </summary>
    public required string Reason { get; init; }
}

