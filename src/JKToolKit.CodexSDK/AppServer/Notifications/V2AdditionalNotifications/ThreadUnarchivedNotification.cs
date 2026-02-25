using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread is unarchived.
/// </summary>
public sealed record class ThreadUnarchivedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadUnarchivedNotification"/>.
    /// </summary>
    public ThreadUnarchivedNotification(string ThreadId, JsonElement Params)
        : base("thread/unarchived", Params)
    {
        this.ThreadId = ThreadId;
    }
}
