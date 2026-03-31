namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a collab wait tool call.
/// </summary>
public sealed record CollabWaitingBeginEvent : CodexEvent
{
    /// <summary>
    /// Gets the sender thread id.
    /// </summary>
    public required string SenderThreadId { get; init; }

    /// <summary>
    /// Gets the receiver thread ids.
    /// </summary>
    public required IReadOnlyList<string> ReceiverThreadIds { get; init; }

    /// <summary>
    /// Gets optional receiver metadata.
    /// </summary>
    public IReadOnlyList<CollabAgentRef>? ReceiverAgents { get; init; }

    /// <summary>
    /// Gets the tool call id associated with this wait.
    /// </summary>
    public required string CallId { get; init; }
}
