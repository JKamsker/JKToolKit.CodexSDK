using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread is closed.
/// </summary>
public sealed record class ThreadClosedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadClosedNotification"/>.
    /// </summary>
    public ThreadClosedNotification(string ThreadId, JsonElement Params)
        : base("thread/closed", Params)
    {
        this.ThreadId = ThreadId;
    }
}

