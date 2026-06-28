using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread is deleted.
/// </summary>
public sealed record class ThreadDeletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadDeletedNotification"/>.
    /// </summary>
    public ThreadDeletedNotification(string ThreadId, JsonElement Params)
        : base("thread/deleted", Params)
    {
        this.ThreadId = ThreadId;
    }
}
