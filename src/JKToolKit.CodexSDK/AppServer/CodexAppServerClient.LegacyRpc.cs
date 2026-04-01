namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Reads a lightweight summary for a conversation by ID or rollout path.
    /// </summary>
    /// <remarks>
    /// This calls the flat-exported app-server method <c>getConversationSummary</c>.
    /// Exactly one of <see cref="ConversationSummaryOptions.ConversationId"/> or
    /// <see cref="ConversationSummaryOptions.RolloutPath"/> must be provided.
    /// </remarks>
    public Task<ConversationSummaryResult> GetConversationSummaryAsync(ConversationSummaryOptions options, CancellationToken ct = default) =>
        _configClient.GetConversationSummaryAsync(options, ct);

    /// <summary>
    /// Produces a diff between the repository at <paramref name="options"/> and its configured remote base.
    /// </summary>
    /// <remarks>
    /// This calls the flat-exported app-server method <c>gitDiffToRemote</c>.
    /// </remarks>
    public Task<GitDiffToRemoteResult> GetGitDiffToRemoteAsync(GitDiffToRemoteOptions options, CancellationToken ct = default) =>
        _configClient.GetGitDiffToRemoteAsync(options, ct);

    /// <summary>
    /// Reads the current OpenAI auth status.
    /// </summary>
    /// <remarks>
    /// This calls the flat-exported app-server method <c>getAuthStatus</c>.
    /// </remarks>
    public Task<AuthStatusReadResult> GetAuthStatusAsync(AuthStatusOptions options, CancellationToken ct = default) =>
        _configClient.GetAuthStatusAsync(options, ct);

    /// <summary>
    /// Reads the current OpenAI auth status with default options.
    /// </summary>
    /// <remarks>
    /// This calls the flat-exported app-server method <c>getAuthStatus</c>.
    /// </remarks>
    public Task<AuthStatusReadResult> GetAuthStatusAsync(CancellationToken ct = default) =>
        _configClient.GetAuthStatusAsync(new AuthStatusOptions(), ct);
}
