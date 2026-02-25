namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Represents completion of an MCP tool call.
/// </summary>
public sealed record McpToolCallEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this MCP invocation.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the MCP server name, when provided.
    /// </summary>
    public string? Server { get; init; }

    /// <summary>
    /// Gets the MCP tool name, when provided.
    /// </summary>
    public string? Tool { get; init; }

    /// <summary>
    /// Gets the execution duration, when provided.
    /// </summary>
    public string? Duration { get; init; }

    /// <summary>
    /// Gets the tool call arguments as JSON, when provided.
    /// </summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>
    /// Gets the tool call result as JSON, when provided.
    /// </summary>
    public string? ResultJson { get; init; }
}
