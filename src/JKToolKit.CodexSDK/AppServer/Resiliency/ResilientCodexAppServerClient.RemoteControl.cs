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
}

#pragma warning restore CS1591
