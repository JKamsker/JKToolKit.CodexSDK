namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a diff emitted for the current turn.
/// </summary>
public sealed record TurnDiffEvent : CodexEvent
{
    /// <summary>
    /// Gets the unified diff for the current turn.
    /// </summary>
    public required string UnifiedDiff { get; init; }
}

