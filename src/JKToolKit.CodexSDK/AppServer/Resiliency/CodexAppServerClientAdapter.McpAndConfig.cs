namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<ConversationSummaryResult> GetConversationSummaryAsync(ConversationSummaryOptions options, CancellationToken ct);

    Task<GitDiffToRemoteResult> GetGitDiffToRemoteAsync(GitDiffToRemoteOptions options, CancellationToken ct);

    Task<AuthStatusReadResult> GetAuthStatusAsync(AuthStatusOptions options, CancellationToken ct);

    Task<AuthStatusReadResult> GetAuthStatusAsync(CancellationToken ct);

    Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct);

    Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct);

    Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct);

    Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct);

    Task<AccountReadResult> ReadAccountAsync(CancellationToken ct);

    Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct);

    Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct);

    Task<ModelListResult> ListModelsAsync(CancellationToken ct);

    Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct);

    Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(CancellationToken ct);

    Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct);

    Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct);

    Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct);

    Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd, CancellationToken ct);

    Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct);

    Task ReloadMcpServersAsync(CancellationToken ct);

    Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct);

    Task<McpResourceReadResult> ReadMcpResourceAsync(McpResourceReadOptions options, CancellationToken ct);

    Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct);

    Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct);

    Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<ConversationSummaryResult> GetConversationSummaryAsync(ConversationSummaryOptions options, CancellationToken ct) => _inner.GetConversationSummaryAsync(options, ct);

    public Task<GitDiffToRemoteResult> GetGitDiffToRemoteAsync(GitDiffToRemoteOptions options, CancellationToken ct) => _inner.GetGitDiffToRemoteAsync(options, ct);

    public Task<AuthStatusReadResult> GetAuthStatusAsync(AuthStatusOptions options, CancellationToken ct) => _inner.GetAuthStatusAsync(options, ct);

    public Task<AuthStatusReadResult> GetAuthStatusAsync(CancellationToken ct) => _inner.GetAuthStatusAsync(ct);

    public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct) => _inner.ReadConfigAsync(options, ct);

    public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct) => _inner.DetectExternalAgentConfigAsync(options, ct);

    public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct) => _inner.ImportExternalAgentConfigAsync(migrationItems, ct);

    public Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct) => _inner.ReadAccountAsync(options, ct);

    public Task<AccountReadResult> ReadAccountAsync(CancellationToken ct) => _inner.ReadAccountAsync(ct);

    public Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct) => _inner.ReadAccountRateLimitsAsync(ct);

    public Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct) => _inner.ListModelsAsync(options, ct);

    public Task<ModelListResult> ListModelsAsync(CancellationToken ct) => _inner.ListModelsAsync(ct);

    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct) => _inner.ListExperimentalFeaturesAsync(options, ct);

    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(CancellationToken ct) => _inner.ListExperimentalFeaturesAsync(ct);

    public Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct) => _inner.WriteConfigValueAsync(options, ct);

    public Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct) => _inner.WriteConfigBatchAsync(options, ct);

    public Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct) => _inner.LogoutAccountAsync(ct);

    public Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct) => _inner.UploadFeedbackAsync(options, ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(options, ct);

    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(mode, cwd, ct);

    public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct) => _inner.StartWindowsSandboxSetupAsync(mode, ct);

    public Task ReloadMcpServersAsync(CancellationToken ct) => _inner.ReloadMcpServersAsync(ct);

    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct) => _inner.ListMcpServerStatusAsync(options, ct);

    public Task<McpResourceReadResult> ReadMcpResourceAsync(McpResourceReadOptions options, CancellationToken ct) => _inner.ReadMcpResourceAsync(options, ct);

    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct) => _inner.StartMcpServerOauthLoginAsync(options, ct);

    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct) => _inner.StartAccountLoginAsync(options, ct);

    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct) => _inner.CancelAccountLoginAsync(loginId, ct);
}
