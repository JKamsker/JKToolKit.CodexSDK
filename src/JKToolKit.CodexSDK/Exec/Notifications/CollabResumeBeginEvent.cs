namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a collab resume tool call.
/// </summary>
public sealed record CollabResumeBeginEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this resume.
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

    /// <summary>
    /// Gets the optional receiver nickname.
    /// </summary>
    public string? ReceiverAgentNickname { get; init; }

    /// <summary>
    /// Gets the optional receiver role.
    /// </summary>
    public string? ReceiverAgentRole { get; init; }
}
