namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting an OAuth login flow for a configured MCP server.
/// </summary>
public sealed class McpServerOauthLoginOptions
{
    /// <summary>
    /// Gets or sets the configured MCP server name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the thread id that the OAuth flow belongs to, when scoped to a thread.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets optional OAuth scopes to request (overrides server defaults).
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets an optional timeout in seconds for the login flow.
    /// </summary>
    public long? TimeoutSeconds { get; set; }
}
