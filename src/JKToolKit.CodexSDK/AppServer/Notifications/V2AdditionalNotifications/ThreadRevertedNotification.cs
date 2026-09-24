using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

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
        : base(AppServerMethods.ThreadReverted, Params)
    {
        this.ThreadId = ThreadId;
    }
}
