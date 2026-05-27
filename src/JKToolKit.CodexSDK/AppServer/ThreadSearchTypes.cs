using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>thread/search</c>.
/// </summary>
public sealed class ThreadSearchOptions
{
    /// <summary>
    /// Gets or sets the required content search term.
    /// </summary>
    public required string SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets an optional archived filter.
    /// </summary>
    public bool? Archived { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets an optional source kind filter.
    /// </summary>
    public IReadOnlyList<string>? SourceKinds { get; set; }

    /// <summary>
    /// Gets or sets an optional pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional sort key, for example <c>created_at</c> or <c>updated_at</c>.
    /// </summary>
    public string? SortKey { get; set; }

    /// <summary>
    /// Gets or sets an optional sort direction, for example <c>asc</c> or <c>desc</c>.
    /// </summary>
    public string? SortDirection { get; set; }
}

/// <summary>
/// Represents one <c>thread/search</c> hit.
/// </summary>
public sealed record class ThreadSearchResult
{
    /// <summary>
    /// Gets the matching thread summary.
    /// </summary>
    public required CodexThreadSummary Thread { get; init; }

    /// <summary>
    /// Gets the result preview snippet.
    /// </summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// Gets the raw result payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a page returned by <c>thread/search</c>.
/// </summary>
public sealed record class ThreadSearchPage
{
    /// <summary>
    /// Gets the search results.
    /// </summary>
    public required IReadOnlyList<ThreadSearchResult> Results { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the backwards cursor token, if any.
    /// </summary>
    public string? BackwardsCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
