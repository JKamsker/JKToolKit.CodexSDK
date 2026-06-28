using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result returned by <c>account/workspaceMessages/read</c>.
/// </summary>
public sealed record class WorkspaceMessagesReadResult
{
    /// <summary>
    /// Gets a value indicating whether the workspace-message backend route is available for this client.
    /// </summary>
    public bool FeatureEnabled { get; init; }

    /// <summary>
    /// Gets active workspace messages returned by the backend.
    /// </summary>
    public required IReadOnlyList<WorkspaceMessageInfo> Messages { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes an active account workspace message.
/// </summary>
public sealed record class WorkspaceMessageInfo
{
    /// <summary>
    /// Gets the backend message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the raw upstream message type string.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Gets the parsed workspace message kind.
    /// </summary>
    public WorkspaceMessageKind MessageKind { get; init; }

    /// <summary>
    /// Gets the message body text.
    /// </summary>
    public required string MessageBody { get; init; }

    /// <summary>
    /// Gets the Unix timestamp, in seconds, when the message was created.
    /// </summary>
    public long? CreatedAt { get; init; }

    /// <summary>
    /// Gets the Unix timestamp, in seconds, when the message was archived.
    /// </summary>
    public long? ArchivedAt { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the message.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Known workspace message types.
/// </summary>
public enum WorkspaceMessageKind
{
    /// <summary>
    /// The server returned a message type unknown to this SDK version.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A workspace notification headline.
    /// </summary>
    Headline,

    /// <summary>
    /// A workspace announcement.
    /// </summary>
    Announcement
}
