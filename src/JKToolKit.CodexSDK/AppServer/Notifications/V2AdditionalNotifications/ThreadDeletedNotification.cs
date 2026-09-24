using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

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
        : base(AppServerMethods.ThreadDeleted, Params)
    {
        this.ThreadId = ThreadId;
    }
}
