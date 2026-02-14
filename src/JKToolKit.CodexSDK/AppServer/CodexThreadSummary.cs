using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed summary of a thread returned by the app-server.
/// </summary>
public sealed record class CodexThreadSummary
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the optional thread name/title, when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the thread is archived, when present.
    /// </summary>
    public bool? Archived { get; init; }

    /// <summary>
    /// Gets the thread creation timestamp, when present.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the working directory associated with the thread, when present.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the model associated with the thread, when present.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the thread summary.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
