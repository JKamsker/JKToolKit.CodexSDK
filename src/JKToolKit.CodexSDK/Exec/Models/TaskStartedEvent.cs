namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the start of a top-level task.
/// </summary>
public sealed record TaskStartedEvent : CodexEvent
{
    public int? ModelContextWindow { get; init; }
}

