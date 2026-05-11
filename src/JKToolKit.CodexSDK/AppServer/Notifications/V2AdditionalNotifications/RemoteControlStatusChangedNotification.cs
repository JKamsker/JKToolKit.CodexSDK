using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when remote-control connection state changes.
/// </summary>
public sealed record class RemoteControlStatusChangedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the remote-control connection status.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets the current remote-control environment id, when available.
    /// </summary>
    public string? EnvironmentId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteControlStatusChangedNotification"/>.
    /// </summary>
    public RemoteControlStatusChangedNotification(string status, string? environmentId, JsonElement @params)
        : base("remoteControl/status/changed", @params)
    {
        Status = status;
        EnvironmentId = environmentId;
    }
}
