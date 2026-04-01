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
    /// Gets the sender thread id.
    /// </summary>
    public required string SenderThreadId { get; init; }

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
    /// Gets the initial prompt that was sent to the new agent.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the effective model used by the spawned agent.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets the effective reasoning effort used by the spawned agent.
    /// </summary>
    public required string ReasoningEffort { get; init; }

    /// <summary>
    /// Gets the last known new-agent status reported to the sender.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets parsed status details, including any payload text/metadata carried by the union.
    /// </summary>
    public CollabAgentStatusInfo? StatusInfo { get; init; }
}
