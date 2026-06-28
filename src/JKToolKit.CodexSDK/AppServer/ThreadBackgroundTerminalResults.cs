using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>thread/backgroundTerminals/list</c>.
/// </summary>
public sealed class ThreadBackgroundTerminalListOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets an optional pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// Represents a background terminal process owned by a thread.
/// </summary>
public sealed record class ThreadBackgroundTerminalInfo
{
    /// <summary>
    /// Gets the response item identifier associated with the process.
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the app-server process identifier.
    /// </summary>
    public required string ProcessId { get; init; }

    /// <summary>
    /// Gets the command that started the process.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets the working directory reported by the server.
    /// </summary>
    public required string Cwd { get; init; }

    /// <summary>
    /// Gets the operating-system process id, when available.
    /// </summary>
    public long? OsPid { get; init; }

    /// <summary>
    /// Gets the sampled CPU percentage, when available.
    /// </summary>
    public double? CpuPercent { get; init; }

    /// <summary>
    /// Gets the sampled resident set size in KiB, when available.
    /// </summary>
    public long? RssKb { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for this process.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>thread/backgroundTerminals/list</c>.
/// </summary>
public sealed record class ThreadBackgroundTerminalListPage
{
    /// <summary>
    /// Gets the background terminal processes returned by the server.
    /// </summary>
    public required IReadOnlyList<ThreadBackgroundTerminalInfo> Data { get; init; }

    /// <summary>
    /// Gets the cursor for the next page, when more data is available.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>thread/backgroundTerminals/terminate</c>.
/// </summary>
public sealed class ThreadBackgroundTerminalTerminateOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the app-server process identifier.
    /// </summary>
    public required string ProcessId { get; set; }
}

/// <summary>
/// Result returned by <c>thread/backgroundTerminals/terminate</c>.
/// </summary>
public sealed record class ThreadBackgroundTerminalTerminateResult
{
    /// <summary>
    /// Gets a value indicating whether the process was terminated.
    /// </summary>
    public bool Terminated { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
