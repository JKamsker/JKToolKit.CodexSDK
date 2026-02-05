namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents an error emitted by Codex during a session.
/// </summary>
public sealed record ErrorEvent : CodexEvent
{
    public required string Message { get; init; }
}

