using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>account/workspaceMessages/read</c>.
/// </summary>
public sealed record class WorkspaceMessagesReadResult
{
    /// <summary>
    /// Gets whether the workspace-message backend route is enabled.
    /// </summary>
    public bool FeatureEnabled { get; init; }

    /// <summary>
    /// Gets active workspace messages.
    /// </summary>
    public required IReadOnlyList<WorkspaceMessage> Messages { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Active workspace message returned by the app-server.
/// </summary>
public sealed record class WorkspaceMessage
{
    /// <summary>
    /// Gets the message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the upstream message type value.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Gets the message body.
    /// </summary>
    public required string MessageBody { get; init; }

    /// <summary>
    /// Gets the creation Unix timestamp in seconds, when available.
    /// </summary>
    public long? CreatedAt { get; init; }

    /// <summary>
    /// Gets the archive Unix timestamp in seconds, when available.
    /// </summary>
    public long? ArchivedAt { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the message.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
