using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>fs/watch</c>.
/// </summary>
public sealed class FsWatchOptions
{
    /// <summary>
    /// Gets or sets the absolute path to watch.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// Result returned by <c>fs/watch</c>.
/// </summary>
public sealed record class FsWatchResult
{
    /// <summary>
    /// Gets the watched path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the watch identifier.
    /// </summary>
    public required string WatchId { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/unwatch</c>.
/// </summary>
public sealed class FsUnwatchOptions
{
    /// <summary>
    /// Gets or sets the watch identifier returned by <c>fs/watch</c>.
    /// </summary>
    public required string WatchId { get; set; }
}

/// <summary>
/// Result returned by <c>fs/unwatch</c>.
/// </summary>
public sealed record class FsUnwatchResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
