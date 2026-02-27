using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when thread realtime transport closes.
/// </summary>
public sealed record class ThreadRealtimeClosedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the close reason, when present.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeClosedNotification"/>.
    /// </summary>
    public ThreadRealtimeClosedNotification(string ThreadId, string? Reason, JsonElement Params)
        : base("thread/realtime/closed", Params)
    {
        this.ThreadId = ThreadId;
        this.Reason = Reason;
    }
}

