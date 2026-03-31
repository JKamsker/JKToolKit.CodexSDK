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
    /// Detects external agent configuration that can be migrated into Codex.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>externalAgentConfig/detect</c>.
    /// </remarks>
    public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct = default) =>
        _configClient.DetectExternalAgentConfigAsync(options, ct);

    /// <summary>
    /// Imports external agent configuration into Codex.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>externalAgentConfig/import</c>.
    /// </remarks>
    public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct = default) =>
        _configClient.ImportExternalAgentConfigAsync(migrationItems, ct);

    /// <summary>
    /// Starts the Windows sandbox setup flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>windowsSandbox/setupStart</c>.
    /// Known modes include <c>elevated</c> and <c>unelevated</c>.
    /// </remarks>
    public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct = default) =>
        _configClient.StartWindowsSandboxSetupAsync(mode, ct);

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

    /// <summary>
    /// Starts an account login flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/login/start</c>.
    /// Supported flows include API-key login, browser ChatGPT login, device-code ChatGPT login, and externally managed
    /// ChatGPT auth-token submission.
    /// </remarks>
    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct = default) =>
        _configClient.StartAccountLoginAsync(options, ct);

    /// <summary>
    /// Cancels a pending account login flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/login/cancel</c>.
    /// </remarks>
    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct = default) =>
        _configClient.CancelAccountLoginAsync(loginId, ct);
}

