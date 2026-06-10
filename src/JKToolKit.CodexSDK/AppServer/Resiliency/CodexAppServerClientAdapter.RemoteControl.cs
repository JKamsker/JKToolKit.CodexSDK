namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct);

    Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct);

    Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct);

    Task<RemoteControlPairingStartResult> StartRemoteControlPairingAsync(RemoteControlPairingStartOptions options, CancellationToken ct);

    Task<RemoteControlPairingStatusResult> ReadRemoteControlPairingStatusAsync(RemoteControlPairingStatusOptions options, CancellationToken ct);

    Task<RemoteControlClientsListResult> ListRemoteControlClientsAsync(RemoteControlClientsListOptions options, CancellationToken ct);

    Task<RemoteControlClientsRevokeResult> RevokeRemoteControlClientAsync(RemoteControlClientsRevokeOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct) =>
        _inner.EnableRemoteControlAsync(ct);

    public Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct) =>
        _inner.DisableRemoteControlAsync(ct);

    public Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct) =>
        _inner.ReadRemoteControlStatusAsync(ct);

    public Task<RemoteControlPairingStartResult> StartRemoteControlPairingAsync(RemoteControlPairingStartOptions options, CancellationToken ct) =>
        _inner.StartRemoteControlPairingAsync(options, ct);

    public Task<RemoteControlPairingStatusResult> ReadRemoteControlPairingStatusAsync(RemoteControlPairingStatusOptions options, CancellationToken ct) =>
        _inner.ReadRemoteControlPairingStatusAsync(options, ct);

    public Task<RemoteControlClientsListResult> ListRemoteControlClientsAsync(RemoteControlClientsListOptions options, CancellationToken ct) =>
        _inner.ListRemoteControlClientsAsync(options, ct);

    public Task<RemoteControlClientsRevokeResult> RevokeRemoteControlClientAsync(RemoteControlClientsRevokeOptions options, CancellationToken ct) =>
        _inner.RevokeRemoteControlClientAsync(options, ct);
}
