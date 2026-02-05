namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents completion of a top-level task.
/// </summary>
public sealed record TaskCompleteEvent : CodexEvent
{
    public string? LastAgentMessage { get; init; }
}

