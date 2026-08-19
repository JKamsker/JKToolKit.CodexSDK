using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a thread's durable history is reverted.
/// </summary>
public sealed record class ThreadRevertedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRevertedNotification"/>.
    /// </summary>
    public ThreadRevertedNotification(string ThreadId, JsonElement Params)
        : base("thread/reverted", Params)
    {
        this.ThreadId = ThreadId;
    }
}
