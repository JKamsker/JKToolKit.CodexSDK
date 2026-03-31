namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents the start of a collab agent spawn tool call.
/// </summary>
public sealed record CollabAgentSpawnBeginEvent : CodexEvent
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
    /// Gets the initial prompt sent to the spawned agent.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the effective model used by the spawned agent.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets the effective reasoning effort used by the spawned agent.
    /// </summary>
    public string? ReasoningEffort { get; init; }
}
