namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing threads via the app-server.
/// </summary>
public sealed class ThreadListOptions
{
    /// <summary>
    /// Gets or sets an optional archived filter.
    /// </summary>
    public bool? Archived { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory filter.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional search query filter, if supported upstream.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets an optional page size, if supported upstream.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets an optional cursor for paging.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional sort key, if supported upstream.
    /// </summary>
    public string? SortKey { get; set; }

    /// <summary>
    /// Gets or sets an optional sort direction (e.g. "asc" / "desc"), if supported upstream.
    /// </summary>
    public string? SortDirection { get; set; }
}

