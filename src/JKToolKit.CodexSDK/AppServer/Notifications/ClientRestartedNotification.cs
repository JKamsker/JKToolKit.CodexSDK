using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// A local (client-side) marker notification emitted by resilient wrappers when the app-server subprocess is restarted.
/// </summary>
public sealed record class ClientRestartedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the restart count for the resilient client lifetime.
    /// </summary>
    public int RestartCount { get; }

    /// <summary>
    /// Gets the previous subprocess exit code, when known.
    /// </summary>
    public int? PreviousExitCode { get; }

    /// <summary>
    /// Gets the restart timestamp in UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the reason string, when available.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets a best-effort tail of stderr captured from the previous subprocess (may be empty).
    /// </summary>
    public IReadOnlyList<string> PreviousStderrTail { get; }

    /// <summary>
    /// Initializes a new marker notification.
    /// </summary>
    public ClientRestartedNotification(
        int restartCount,
        int? previousExitCode,
        DateTimeOffset timestamp,
        string? reason,
        IReadOnlyList<string>? previousStderrTail)
        : base(
            method: "client/restarted",
            @params: JsonSerializer.SerializeToElement(new
            {
                restartCount,
                previousExitCode,
                timestamp = timestamp.UtcDateTime,
                reason,
                previousStderrTail = previousStderrTail ?? Array.Empty<string>()
            }).Clone())
    {
        RestartCount = restartCount;
        PreviousExitCode = previousExitCode;
        Timestamp = timestamp;
        Reason = reason;
        PreviousStderrTail = previousStderrTail ?? Array.Empty<string>();
    }
}

