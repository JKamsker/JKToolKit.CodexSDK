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
}
