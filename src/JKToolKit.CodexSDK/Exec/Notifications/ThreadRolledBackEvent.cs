namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents a rollback where the last N user turns were removed from the thread context.
/// </summary>
public sealed record ThreadRolledBackEvent : CodexEvent
{
    /// <summary>
    /// Gets the number of user turns removed.
    /// </summary>
    public required int NumTurns { get; init; }
}

