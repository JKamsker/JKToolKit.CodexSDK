using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a fuzzy file search session is completed.
/// </summary>
public sealed record class FuzzyFileSearchSessionCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FuzzyFileSearchSessionCompletedNotification"/>.
    /// </summary>
    public FuzzyFileSearchSessionCompletedNotification(string sessionId, JsonElement @params)
        : base("fuzzyFileSearch/sessionCompleted", @params)
    {
        SessionId = sessionId;
    }
}
