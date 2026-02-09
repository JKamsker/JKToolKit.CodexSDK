using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server reports that context compaction has completed.
/// </summary>
public sealed record class ContextCompactedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier associated with the compaction event.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ContextCompactedNotification"/>.
    /// </summary>
    public ContextCompactedNotification(string ThreadId, string TurnId, JsonElement Params)
        : base("thread/compacted", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
    }
}
