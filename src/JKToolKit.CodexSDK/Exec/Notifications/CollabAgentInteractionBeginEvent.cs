namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a collab agent interaction tool call.
/// </summary>
public sealed record CollabAgentInteractionBeginEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this interaction.
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
    /// Gets the prompt sent from the sender to the receiver.
    /// </summary>
    public required string Prompt { get; init; }
}
