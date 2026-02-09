namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Controls restart backoff and limits for a resilient app-server client.
/// </summary>
public sealed record class CodexAppServerRestartPolicy
{
    /// <summary>
    /// Gets the maximum number of restarts allowed within <see cref="Window"/>.
    /// </summary>
    public int MaxRestarts { get; init; } = 5;

    /// <summary>
    /// Gets the sliding window used for restart limiting.
    /// </summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the initial backoff delay applied after repeated restarts.
    /// </summary>
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets the maximum backoff delay.
    /// </summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the jitter fraction applied to backoff delays. A value of 0.2 means ±20%.
    /// </summary>
    public double JitterFraction { get; init; } = 0.2;

    /// <summary>
    /// Gets a default bounded restart policy (5 restarts per 60 seconds, exponential backoff capped at 10s).
    /// </summary>
    public static CodexAppServerRestartPolicy Default { get; } = new();
}

