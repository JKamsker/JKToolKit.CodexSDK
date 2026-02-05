namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents an aborted turn (e.g. interrupted).
/// </summary>
public sealed record TurnAbortedEvent : CodexEvent
{
    public required string Reason { get; init; }
}

