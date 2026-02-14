namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing loaded thread identifiers via the app-server.
/// </summary>
public sealed class ThreadLoadedListOptions
{
    /// <summary>
    /// Gets or sets an optional cursor for paging.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size, if supported upstream.
    /// </summary>
    public int? Limit { get; set; }
}

