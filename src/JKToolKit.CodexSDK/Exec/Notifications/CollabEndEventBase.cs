namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Base type for collab tool call completion events that report sender/receiver metadata.
/// </summary>
public abstract record CollabEndEventBase : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this collab tool call.
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
    /// Gets the receiver agent nickname, when provided.
    /// </summary>
    public string? ReceiverAgentNickname { get; init; }

    /// <summary>
    /// Gets the receiver agent role, when provided.
    /// </summary>
    public string? ReceiverAgentRole { get; init; }

    /// <summary>
    /// Gets the last known receiver status reported to the sender.
    /// </summary>
    /// <remarks>
    /// <see cref="CollabReceiverStatus.Unknown"/> indicates the status was missing or unrecognized.
    /// </remarks>
    public CollabReceiverStatus Status { get; init; } = CollabReceiverStatus.Unknown;

    /// <summary>
    /// Gets parsed status details, including any payload text/metadata carried by the union.
    /// </summary>
    public CollabAgentStatusInfo? StatusInfo { get; init; }
}
