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
}

