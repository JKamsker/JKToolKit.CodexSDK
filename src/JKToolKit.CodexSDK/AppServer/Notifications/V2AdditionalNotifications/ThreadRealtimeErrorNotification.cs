using System.Text.Json;

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
        : base("thread/realtime/error", Params)
    {
        this.ThreadId = ThreadId;
        this.Message = Message;
    }
}

