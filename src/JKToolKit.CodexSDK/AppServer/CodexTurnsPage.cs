using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a paged collection of thread turns.
/// </summary>
public sealed record class CodexTurnsPage
{
    /// <summary>
    /// Gets the returned turns.
    /// </summary>
    public required IReadOnlyList<CodexTurn> Data { get; init; }

    /// <summary>
    /// Gets the cursor for the next page, when present.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the cursor for paging in the reverse direction, when present.
    /// </summary>
    public string? BackwardsCursor { get; init; }

    /// <summary>
    /// Gets the raw page payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
