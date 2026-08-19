using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread's queued submissions change.
/// </summary>
public sealed record class ThreadQueueChangedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadQueueChangedNotification"/>.
    /// </summary>
    public ThreadQueueChangedNotification(string ThreadId, JsonElement Params)
        : base("thread/queue/changed", Params)
    {
        this.ThreadId = ThreadId;
    }
}
