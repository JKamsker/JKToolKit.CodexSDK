namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of an undo operation.
/// </summary>
public sealed record UndoCompletedEvent : CodexEvent
{
    /// <summary>
    /// Gets whether the undo operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets an optional message describing the undo result.
    /// </summary>
    public string? Message { get; init; }
}

