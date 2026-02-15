namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing apps/connectors via the app-server.
/// </summary>
public sealed class AppsListOptions
{
    /// <summary>
    /// Gets or sets an optional working directory scope.
    /// </summary>
    /// <remarks>
    /// Newer upstream Codex app-server builds do not accept a <c>cwd</c> parameter for <c>app/list</c>.
    /// Prefer <see cref="ThreadId"/> when you need request scoping.
    /// </remarks>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional cursor for paging.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size, if supported upstream.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets an optional thread id used to evaluate app feature gating from that thread's config.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass caches and refetch app metadata.
    /// </summary>
    public bool ForceRefetch { get; set; }
}

