using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when thread realtime startup is accepted.
/// </summary>
public sealed record class ThreadRealtimeStartedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the realtime session identifier, when present.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets the realtime protocol version selected by the server.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeStartedNotification"/>.
    /// </summary>
    public ThreadRealtimeStartedNotification(string ThreadId, string? SessionId, string Version, JsonElement Params)
        : base("thread/realtime/started", Params)
    {
        this.ThreadId = ThreadId;
        this.SessionId = SessionId;
        this.Version = Version;
    }
}
