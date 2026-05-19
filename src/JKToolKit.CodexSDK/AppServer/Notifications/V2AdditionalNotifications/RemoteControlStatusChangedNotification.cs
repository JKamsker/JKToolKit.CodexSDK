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
    /// Gets the typed remote-control connection status.
    /// </summary>
    public RemoteControlConnectionStatus StatusValue { get; }

    /// <summary>
    /// Gets the remote-control server name.
    /// </summary>
    public string? ServerName { get; }

    /// <summary>
    /// Gets the remote-control installation identifier.
    /// </summary>
    public string? InstallationId { get; }

    /// <summary>
    /// Gets the current remote-control environment id, when available.
    /// </summary>
    public string? EnvironmentId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteControlStatusChangedNotification"/>.
    /// </summary>
    public RemoteControlStatusChangedNotification(
        string status,
        string? serverName,
        string? installationId,
        string? environmentId,
        JsonElement @params)
        : base("remoteControl/status/changed", @params)
    {
        Status = status;
        StatusValue = RemoteControlConnectionStatus.Parse(status);
        ServerName = serverName;
        InstallationId = installationId;
        EnvironmentId = environmentId;
    }
}
