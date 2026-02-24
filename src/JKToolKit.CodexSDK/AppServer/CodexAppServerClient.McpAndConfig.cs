namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Reads the effective merged configuration (with optional layer details).
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>config/read</c>. If <see cref="ConfigReadOptions.Cwd"/> is set,
    /// Codex resolves project config layers as seen from that directory.
    /// </remarks>
    public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct = default) =>
        _configClient.ReadConfigAsync(options, ct);

    /// <summary>
    /// Reloads MCP server configuration from disk and queues a refresh for loaded threads.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>config/mcpServer/reload</c>.
    /// Refresh is applied on each thread's next active turn.
    /// </remarks>
    public Task ReloadMcpServersAsync(CancellationToken ct = default) =>
        _mcpClient.ReloadMcpServersAsync(ct);

    /// <summary>
    /// Lists MCP servers with their tools/resources and auth status.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServerStatus/list</c>.
    /// </remarks>
    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct = default) =>
        _mcpClient.ListMcpServerStatusAsync(options, ct);

    /// <summary>
    /// Starts an OAuth login flow for a configured MCP server.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServer/oauth/login</c>.
    /// The server later emits <c>mcpServer/oauthLogin/completed</c>.
    /// </remarks>
    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default) =>
        _mcpClient.StartMcpServerOauthLoginAsync(options, ct);
}

