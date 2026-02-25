namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;

/// <summary>
/// Represents completion of a web search request.
/// </summary>
public sealed record WebSearchEndEvent : CodexEvent
{
    /// <summary>
    /// Gets the tool call id associated with this web search.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the query string for the completed web search, when provided.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the web search action, when provided.
    /// </summary>
    public WebSearchAction? Action { get; init; }
}
