namespace NCodexSDK.Public;

/// <summary>
/// Result of a non-interactive <c>codex review</c> invocation.
/// </summary>
public sealed record CodexReviewResult
(
    int ExitCode,
    string StandardOutput,
    string StandardError
);

