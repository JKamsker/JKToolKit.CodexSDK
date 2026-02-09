namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Options for enabling resiliency behaviors (auto-restart + optional retries) when using <c>codex app-server</c>.
/// </summary>
public sealed class CodexAppServerResilienceOptions
{
    /// <summary>
    /// Gets or sets whether the resilient client should automatically restart the subprocess when it disconnects.
    /// </summary>
    public bool AutoRestart { get; set; } = true;

    /// <summary>
    /// Gets or sets whether <see cref="ResilientCodexAppServerClient.Notifications"/> should continue across restarts.
    /// </summary>
    public bool NotificationsContinueAcrossRestarts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to emit a local <c>client/restarted</c> marker notification after a restart.
    /// </summary>
    public bool EmitRestartMarkerNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets restart limit and backoff settings.
    /// </summary>
    public CodexAppServerRestartPolicy RestartPolicy { get; set; } = CodexAppServerRestartPolicy.Default;

    /// <summary>
    /// Gets or sets the retry policy for user operations that fail due to disconnect.
    /// </summary>
    /// <remarks>
    /// The default policy never retries, because automatically retrying requests can be unsafe depending
    /// on the operation semantics. Library users can opt into retries as needed.
    /// </remarks>
    public CodexAppServerRetryPolicyDelegate RetryPolicy { get; set; } = CodexAppServerRetryPolicy.NeverRetry;

    /// <summary>
    /// Optional callback invoked after a restart completes (best-effort).
    /// </summary>
    public Action<CodexAppServerRestartEvent>? OnRestart { get; set; }
}

