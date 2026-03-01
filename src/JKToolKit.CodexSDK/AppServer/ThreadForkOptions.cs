namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for forking an existing thread via the app-server.
/// </summary>
public sealed class ThreadForkOptions
{
    /// <summary>
    /// Gets or sets the thread identifier to fork.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// [UNSTABLE] Gets or sets a rollout path to fork from (experimental-gated in newer upstream Codex builds).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to persist additional rollout event variants required to reconstruct
    /// a richer thread history on subsequent resume/fork/read (experimental).
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public bool PersistExtendedHistory { get; set; }
}

