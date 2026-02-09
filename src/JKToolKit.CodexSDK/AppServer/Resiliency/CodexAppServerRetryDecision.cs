namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Decision returned by a retry policy.
/// </summary>
public sealed record class CodexAppServerRetryDecision
{
    /// <summary>
    /// Gets whether the operation should be retried.
    /// </summary>
    public required bool ShouldRetry { get; init; }

    /// <summary>
    /// Gets an optional delay to wait before retrying.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Gets an optional hook to run before retrying (e.g. re-resume a thread).
    /// </summary>
    public Func<CancellationToken, Task>? BeforeRetryAsync { get; init; }

    /// <summary>
    /// Gets an optional reason for diagnostics/telemetry.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// A decision that indicates no retry.
    /// </summary>
    public static CodexAppServerRetryDecision NoRetry { get; } = new() { ShouldRetry = false };

    /// <summary>
    /// Creates a retry decision.
    /// </summary>
    public static CodexAppServerRetryDecision Retry(
        TimeSpan? delay = null,
        Func<CancellationToken, Task>? beforeRetryAsync = null,
        string? reason = null) =>
        new() { ShouldRetry = true, Delay = delay, BeforeRetryAsync = beforeRetryAsync, Reason = reason };
}

