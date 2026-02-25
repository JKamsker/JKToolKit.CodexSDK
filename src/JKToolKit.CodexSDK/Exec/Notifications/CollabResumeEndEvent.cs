namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a collab agent resume tool call.
/// </summary>
public sealed record CollabResumeEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this resume.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the sender thread id, when provided.
    /// </summary>
    public string? SenderThreadId { get; init; }

    /// <summary>
    /// Gets the receiver thread id, when provided.
    /// </summary>
    public string? ReceiverThreadId { get; init; }

    /// <summary>
    /// Gets the receiver agent nickname, when provided.
    /// </summary>
    public string? ReceiverAgentNickname { get; init; }

    /// <summary>
    /// Gets the receiver agent role, when provided.
    /// </summary>
    public string? ReceiverAgentRole { get; init; }

    /// <summary>
    /// Gets the last known receiver status reported to the sender, when provided.
    /// </summary>
    public string? Status { get; init; }
}
