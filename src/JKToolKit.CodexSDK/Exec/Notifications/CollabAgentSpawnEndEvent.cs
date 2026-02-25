namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a collab agent spawn tool call.
/// </summary>
public sealed record CollabAgentSpawnEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this spawn.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the sender thread id, when provided.
    /// </summary>
    public string? SenderThreadId { get; init; }

    /// <summary>
    /// Gets the new thread id, when provided.
    /// </summary>
    public string? NewThreadId { get; init; }

    /// <summary>
    /// Gets the nickname assigned to the new agent, when provided.
    /// </summary>
    public string? NewAgentNickname { get; init; }

    /// <summary>
    /// Gets the role assigned to the new agent, when provided.
    /// </summary>
    public string? NewAgentRole { get; init; }

    /// <summary>
    /// Gets the initial prompt that was sent to the new agent, when provided.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Gets the last known new-agent status reported to the sender, when provided.
    /// </summary>
    public string? Status { get; init; }
}
