using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents one workspace message returned by <c>account/workspaceMessages/read</c>.
/// </summary>
public sealed record class WorkspaceMessage
{
    /// <summary>
    /// Gets the message identifier.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the workspace identifier.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the upstream message type.
    /// </summary>
    public string? MessageType { get; init; }

    /// <summary>
    /// Gets the message text.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the raw message payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the result returned by <c>account/workspaceMessages/read</c>.
/// </summary>
public sealed record class WorkspaceMessagesReadResult
{
    /// <summary>
    /// Gets the workspace messages.
    /// </summary>
    public required IReadOnlyList<WorkspaceMessage> Messages { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
