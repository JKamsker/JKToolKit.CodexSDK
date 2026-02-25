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
    /// Gets or sets an optional substring filter for the extracted thread title, if supported upstream.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets an optional page size, if supported upstream.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets an optional model provider filter, if supported upstream.
    /// </summary>
    public IReadOnlyList<string>? ModelProviders { get; set; }

    /// <summary>
    /// Gets or sets an optional source kind filter, if supported upstream.
    /// </summary>
    public IReadOnlyList<string>? SourceKinds { get; set; }

    /// <summary>
    /// Gets or sets an optional cursor for paging.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional sort key, if supported upstream (for example, <c>created_at</c> or <c>updated_at</c>).
    /// </summary>
    public string? SortKey { get; set; }
}

