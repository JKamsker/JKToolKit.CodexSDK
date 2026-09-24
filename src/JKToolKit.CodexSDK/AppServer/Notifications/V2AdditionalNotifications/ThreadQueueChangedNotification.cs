using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

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
        : base(AppServerMethods.ThreadQueueChanged, Params)
    {
        this.ThreadId = ThreadId;
    }
}
