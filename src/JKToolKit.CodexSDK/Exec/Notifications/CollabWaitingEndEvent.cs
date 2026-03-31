namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of a collab wait tool call.
/// </summary>
public sealed record CollabWaitingEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this wait.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the sender thread id, when provided.
    /// </summary>
    public string? SenderThreadId { get; init; }

    /// <summary>
    /// Gets the last known statuses for receiver agents, when provided.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Statuses { get; init; }

    /// <summary>
    /// Gets structured receiver status details keyed by thread id, when provided.
    /// </summary>
    public IReadOnlyDictionary<string, CollabAgentStatusInfo>? StatusInfos { get; init; }

    /// <summary>
    /// Gets receiver metadata paired with final statuses, when provided.
    /// </summary>
    public IReadOnlyList<CollabAgentStatusEntry>? AgentStatuses { get; init; }
}
