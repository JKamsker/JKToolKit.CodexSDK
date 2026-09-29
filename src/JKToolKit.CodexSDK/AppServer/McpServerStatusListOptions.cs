namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing MCP server status entries via the app-server.
/// </summary>
public sealed class McpServerStatusListOptions
{
    /// <summary>
    /// Gets or sets an optional pagination cursor returned by a previous call.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the optional response detail level.
    /// </summary>
    public McpServerStatusDetail? Detail { get; set; }

    /// <summary>
    /// Gets or sets an optional thread id used to include thread/project-scoped MCP configuration.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets an optional server name used to limit discovery to one MCP server.
    /// When paired with <see cref="ThreadId"/>, the app-server reuses that thread's MCP connection.
    /// </summary>
    public string? ServerName { get; set; }
}
