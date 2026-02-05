namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a background status/update message from Codex.
/// </summary>
public sealed record BackgroundEvent : CodexEvent
{
    public required string Message { get; init; }
}

