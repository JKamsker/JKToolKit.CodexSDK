#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<ConversationSummaryResult> GetConversationSummaryAsync(ConversationSummaryOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.GetConversationSummaryAsync(options, token), ct);

    public Task<GitDiffToRemoteResult> GetGitDiffToRemoteAsync(GitDiffToRemoteOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.GetGitDiffToRemoteAsync(options, token), ct);

    public Task<AuthStatusReadResult> GetAuthStatusAsync(AuthStatusOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.GetAuthStatusAsync(options, token), ct);

    public Task<AuthStatusReadResult> GetAuthStatusAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.GetAuthStatusAsync(token), ct);

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

    public Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ListModelsAsync(options, token), ct);

    public Task<ModelListResult> ListModelsAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ListModelsAsync(token), ct);

    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ListExperimentalFeaturesAsync(options, token), ct);

    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.ListExperimentalFeaturesAsync(token), ct);

    public Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.WriteConfigValueAsync(options, token), ct);

    public Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.WriteConfigBatchAsync(options, token), ct);

    public Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.LogoutAccountAsync(token), ct);

    public Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.UploadFeedbackAsync(options, token), ct);

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

    public Task<McpResourceReadResult> ReadMcpResourceAsync(McpResourceReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Mcp, (c, token) => c.ReadMcpResourceAsync(options, token), ct);

    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Mcp, (c, token) => c.StartMcpServerOauthLoginAsync(options, token), ct);

    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.StartAccountLoginAsync(options, token), ct);

    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Configuration, (c, token) => c.CancelAccountLoginAsync(loginId, token), ct);
}

#pragma warning restore CS1591
