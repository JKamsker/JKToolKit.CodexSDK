namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for including a recent turns page in a <c>thread/resume</c> response.
/// </summary>
public sealed class ThreadResumeInitialTurnsPageOptions
{
    /// <summary>
    /// Gets or sets the optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the optional sort direction. Known values are <c>asc</c> and <c>desc</c>.
    /// </summary>
    public string? SortDirection { get; set; }

    /// <summary>
    /// Gets or sets the optional item detail level. Known values are <c>summary</c> and <c>full</c>.
    /// </summary>
    public string? ItemsView { get; set; }
}
