namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a diff emitted for the current turn.
/// </summary>
public sealed record TurnDiffEvent : CodexEvent
{
    public required string UnifiedDiff { get; init; }
}

