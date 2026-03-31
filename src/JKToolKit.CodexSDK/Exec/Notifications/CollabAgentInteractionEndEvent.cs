namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a collab agent interaction (send_input) tool call.
/// </summary>
public sealed record CollabAgentInteractionEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this collab interaction.
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
    /// Gets the prompt that was sent.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the last known receiver status reported to the sender.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets parsed status details, including any payload text/metadata carried by the union.
    /// </summary>
    public CollabAgentStatusInfo? StatusInfo { get; init; }
}
