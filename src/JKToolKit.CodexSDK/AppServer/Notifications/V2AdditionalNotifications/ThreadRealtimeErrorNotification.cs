using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when thread realtime encounters an error.
/// </summary>
public sealed record class ThreadRealtimeErrorNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeErrorNotification"/>.
    /// </summary>
    public ThreadRealtimeErrorNotification(string ThreadId, string Message, JsonElement Params)
        : base(AppServerMethods.ThreadRealtimeError, Params)
    {
        this.ThreadId = ThreadId;
        this.Message = Message;
    }
}
