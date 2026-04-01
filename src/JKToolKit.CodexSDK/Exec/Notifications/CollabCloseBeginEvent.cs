namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a collab close tool call.
/// </summary>
public sealed record CollabCloseBeginEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this close.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the sender thread id.
    /// </summary>
    public required string SenderThreadId { get; init; }

    /// <summary>
    /// Gets the receiver thread id.
    /// </summary>
    public required string ReceiverThreadId { get; init; }
}
