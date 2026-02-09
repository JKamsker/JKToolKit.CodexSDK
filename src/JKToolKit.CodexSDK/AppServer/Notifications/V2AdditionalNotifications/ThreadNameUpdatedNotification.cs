using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when the server updates a thread's display name.
/// </summary>
public sealed record class ThreadNameUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the new thread name, if any.
    /// </summary>
    public string? ThreadName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadNameUpdatedNotification"/>.
    /// </summary>
    public ThreadNameUpdatedNotification(string ThreadId, string? ThreadName, JsonElement Params)
        : base("thread/name/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.ThreadName = ThreadName;
    }
}
