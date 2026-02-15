using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a fuzzy file search session has updated results.
/// </summary>
public sealed record class FuzzyFileSearchSessionUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the current query.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Gets the returned file matches.
    /// </summary>
    public IReadOnlyList<FuzzyFileSearchResult> Files { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FuzzyFileSearchSessionUpdatedNotification"/>.
    /// </summary>
    public FuzzyFileSearchSessionUpdatedNotification(string sessionId, string query, IReadOnlyList<FuzzyFileSearchResult> files, JsonElement @params)
        : base("fuzzyFileSearch/sessionUpdated", @params)
    {
        SessionId = sessionId;
        Query = query;
        Files = files;
    }
}
