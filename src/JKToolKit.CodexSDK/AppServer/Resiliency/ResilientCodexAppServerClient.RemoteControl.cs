#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.EnableRemoteControlAsync(token), ct);

    public Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.DisableRemoteControlAsync(token), ct);

    public Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.ReadRemoteControlStatusAsync(token), ct);

    public Task<RemoteControlPairingStartResult> StartRemoteControlPairingAsync(RemoteControlPairingStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.StartRemoteControlPairingAsync(options, token), ct);

    public Task<RemoteControlPairingStatusResult> ReadRemoteControlPairingStatusAsync(RemoteControlPairingStatusOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.ReadRemoteControlPairingStatusAsync(options, token), ct);

    public Task<RemoteControlClientsListResult> ListRemoteControlClientsAsync(RemoteControlClientsListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.ListRemoteControlClientsAsync(options, token), ct);

    public Task<RemoteControlClientsRevokeResult> RevokeRemoteControlClientAsync(RemoteControlClientsRevokeOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.RemoteControl, (c, token) => c.RevokeRemoteControlClientAsync(options, token), ct);
}

#pragma warning restore CS1591
