using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread is archived.
/// </summary>
public sealed record class ThreadArchivedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadArchivedNotification"/>.
    /// </summary>
    public ThreadArchivedNotification(string ThreadId, JsonElement Params)
        : base("thread/archived", Params)
    {
        this.ThreadId = ThreadId;
    }
}
