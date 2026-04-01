using System.Text.Json;

namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Parsed representation of a collab agent-status union.
/// </summary>
public sealed record CollabAgentStatusInfo
{
    /// <summary>
    /// Gets the normalized status discriminator.
    /// </summary>
    public CollabAgentStatus Status { get; init; } = CollabAgentStatus.Unknown;

    /// <summary>
    /// Gets the text payload carried by the status union, when provided.
    /// </summary>
    public string? PayloadText { get; init; }

    /// <summary>
    /// Gets the raw structured payload carried by the status union, when provided.
    /// </summary>
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// Metadata describing a collab receiver/new agent.
/// </summary>
public record CollabAgentRef
{
    /// <summary>
    /// Gets the receiver/new-agent thread id.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the optional agent nickname.
    /// </summary>
    public string? AgentNickname { get; init; }

    /// <summary>
    /// Gets the optional agent role.
    /// </summary>
    public string? AgentRole { get; init; }
}

/// <summary>
/// Receiver metadata paired with a final collab status.
/// </summary>
public sealed record CollabAgentStatusEntry : CollabAgentRef
{
    /// <summary>
    /// Gets the parsed status details.
    /// </summary>
    public required CollabAgentStatusInfo StatusInfo { get; init; }
}
