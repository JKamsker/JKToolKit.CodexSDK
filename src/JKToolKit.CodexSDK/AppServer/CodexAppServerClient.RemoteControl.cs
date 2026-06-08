namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Enables remote-control connectivity.
    /// </summary>
    public Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct = default) =>
        _remoteControlClient.EnableAsync(ct);

    /// <summary>
    /// Disables remote-control connectivity.
    /// </summary>
    public Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct = default) =>
        _remoteControlClient.DisableAsync(ct);

    /// <summary>
    /// Reads remote-control connectivity status.
    /// </summary>
    public Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct = default) =>
        _remoteControlClient.ReadStatusAsync(ct);

    /// <summary>
    /// Starts remote-control pairing.
    /// </summary>
    public Task<RemoteControlPairingStartResult> StartRemoteControlPairingAsync(RemoteControlPairingStartOptions options, CancellationToken ct = default) =>
        _remoteControlClient.StartPairingAsync(options, ct);

    /// <summary>
    /// Lists remote-control client grants.
    /// </summary>
    public Task<RemoteControlClientsListResult> ListRemoteControlClientsAsync(RemoteControlClientsListOptions options, CancellationToken ct = default) =>
        _remoteControlClient.ListClientsAsync(options, ct);

    /// <summary>
    /// Revokes a remote-control client grant.
    /// </summary>
    public Task<RemoteControlClientsRevokeResult> RevokeRemoteControlClientAsync(RemoteControlClientsRevokeOptions options, CancellationToken ct = default) =>
        _remoteControlClient.RevokeClientAsync(options, ct);
}
