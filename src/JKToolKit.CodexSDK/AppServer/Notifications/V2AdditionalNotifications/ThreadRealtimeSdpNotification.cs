using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted with the remote SDP for a WebRTC realtime session.
/// </summary>
public sealed record class ThreadRealtimeSdpNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the remote answer SDP.
    /// </summary>
    public string Sdp { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeSdpNotification"/>.
    /// </summary>
    public ThreadRealtimeSdpNotification(string threadId, string sdp, JsonElement @params)
        : base("thread/realtime/sdp", @params)
    {
        ThreadId = threadId;
        Sdp = sdp;
    }
}
