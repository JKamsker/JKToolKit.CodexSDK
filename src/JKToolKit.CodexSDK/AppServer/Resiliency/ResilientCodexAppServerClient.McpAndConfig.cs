#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ReadConfigAsync(options, token), ct);

    public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.DetectExternalAgentConfigAsync(options, token), ct);

    public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ImportExternalAgentConfigAsync(migrationItems, token), ct);

    public Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ReadAccountAsync(options, token), ct);

    public Task<AccountReadResult> ReadAccountAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ReadAccountAsync(token), ct);

    public Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ReadAccountRateLimitsAsync(token), ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.StartWindowsSandboxSetupAsync(options, token), ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd = null, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.StartWindowsSandboxSetupAsync(mode, cwd, token), ct);

    public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.StartWindowsSandboxSetupAsync(mode, token), ct);

    public Task ReloadMcpServersAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Mcp, (c, token) => c.ReloadMcpServersAsync(token), ct);

    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Mcp, (c, token) => c.ListMcpServerStatusAsync(options, token), ct);

    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Mcp, (c, token) => c.StartMcpServerOauthLoginAsync(options, token), ct);

    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.StartAccountLoginAsync(options, token), ct);

    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.CancelAccountLoginAsync(loginId, token), ct);
}

#pragma warning restore CS1591
