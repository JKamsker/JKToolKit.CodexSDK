using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a page of threads returned by the app-server.
/// </summary>
public sealed record class CodexThreadListPage
{
    /// <summary>
    /// Gets the threads returned for this page.
    /// </summary>
    public required IReadOnlyList<CodexThreadSummary> Threads { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

