using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.Public;

/// <summary>
/// Result of a non-interactive <c>codex review</c> invocation.
/// </summary>
public sealed record CodexReviewResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    /// <summary>
    /// Gets the session id if it could be determined from Codex output.
    /// </summary>
    public SessionId? SessionId { get; init; }

    /// <summary>
    /// Gets the resolved session log path if it could be determined.
    /// </summary>
    public string? LogPath { get; init; }
}
