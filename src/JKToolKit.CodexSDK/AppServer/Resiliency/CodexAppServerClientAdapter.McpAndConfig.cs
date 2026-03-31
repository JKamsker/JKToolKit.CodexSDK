namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct);

    Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct);

    Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct);

    Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct);

    Task<AccountReadResult> ReadAccountAsync(CancellationToken ct);

    Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd, CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct);

    Task ReloadMcpServersAsync(CancellationToken ct);

    Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct);

    Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct);

    Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct);

    Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct) => _inner.ReadConfigAsync(options, ct);

    public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct) => _inner.DetectExternalAgentConfigAsync(options, ct);

    public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct) => _inner.ImportExternalAgentConfigAsync(migrationItems, ct);

    public Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct) => _inner.ReadAccountAsync(options, ct);

    public Task<AccountReadResult> ReadAccountAsync(CancellationToken ct) => _inner.ReadAccountAsync(ct);

    public Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct) => _inner.ReadAccountRateLimitsAsync(ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(options, ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(mode, cwd, ct);

    public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(mode, ct);

    public Task ReloadMcpServersAsync(CancellationToken ct) => _inner.ReloadMcpServersAsync(ct);

    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct) => _inner.ListMcpServerStatusAsync(options, ct);

    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct) => _inner.StartMcpServerOauthLoginAsync(options, ct);

    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct) => _inner.StartAccountLoginAsync(options, ct);

    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct) => _inner.CancelAccountLoginAsync(loginId, ct);
}
