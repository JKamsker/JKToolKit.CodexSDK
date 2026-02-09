namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Describes an app-server restart performed by a resilient client.
/// </summary>
public sealed record class CodexAppServerRestartEvent
{
    /// <summary>
    /// Gets the cumulative restart count for the lifetime of the resilient client.
    /// </summary>
    public required int RestartCount { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the restart completed.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the previous subprocess exit code, when known.
    /// </summary>
    public int? PreviousExitCode { get; init; }

    /// <summary>
    /// Gets a short reason string for why the restart happened, when available.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets a best-effort tail of stderr captured from the previous subprocess, when available.
    /// </summary>
    public IReadOnlyList<string> PreviousStderrTail { get; init; } = Array.Empty<string>();
}

