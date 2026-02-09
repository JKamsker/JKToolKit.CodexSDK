using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>
/// Facade for the <c>codex exec</c> mode.
/// </summary>
/// <remarks>
/// This facade is a thin delegation layer over <see cref="ICodexClient"/> intended to provide
/// a consistent surface area under <see cref="CodexSdk"/>.
/// </remarks>
public sealed class CodexExecFacade
{
    private readonly ICodexClient _client;

    /// <summary>
    /// Creates a new facade over an existing <see cref="ICodexClient"/>.
    /// </summary>
    /// <param name="client">The underlying Codex client.</param>
    public CodexExecFacade(ICodexClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Starts a new Codex session with the specified options.
    /// </summary>
    public Task<ICodexSessionHandle> StartSessionAsync(
        CodexSessionOptions options,
        CancellationToken ct = default) =>
        _client.StartSessionAsync(options, ct);

    /// <summary>
    /// Resumes a Codex session by launching the CLI in <c>resume</c> mode.
    /// </summary>
    public Task<ICodexSessionHandle> ResumeSessionAsync(
        SessionId sessionId,
        CodexSessionOptions options,
        CancellationToken ct = default) =>
        _client.ResumeSessionAsync(sessionId, options, ct);

    /// <summary>
    /// Resumes an existing Codex session for read-only access by its session ID.
    /// </summary>
    public Task<ICodexSessionHandle> ResumeSessionAsync(
        SessionId sessionId,
        CancellationToken ct = default) =>
        _client.ResumeSessionAsync(sessionId, ct);

    /// <summary>
    /// Attaches to an existing session log file for reading events.
    /// </summary>
    public Task<ICodexSessionHandle> AttachToLogAsync(
        string logFilePath,
        CancellationToken ct = default) =>
        _client.AttachToLogAsync(logFilePath, ct);

    /// <summary>
    /// Enumerates sessions that match the specified filter criteria.
    /// </summary>
    public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        SessionFilter? filter = null,
        CancellationToken ct = default) =>
        _client.ListSessionsAsync(filter, ct);

    /// <summary>
    /// Retrieves the most recent rate limit information emitted by Codex.
    /// </summary>
    public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken ct = default) =>
        _client.GetRateLimitsAsync(noCache, ct);

    /// <summary>
    /// Runs a non-interactive code review using <c>codex review</c>.
    /// </summary>
    public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken ct = default) =>
        _client.ReviewAsync(options, ct);
}
