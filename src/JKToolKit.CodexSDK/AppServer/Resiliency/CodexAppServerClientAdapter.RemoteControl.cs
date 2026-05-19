namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct);

    Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct);

    Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<RemoteControlStatusResult> EnableRemoteControlAsync(CancellationToken ct) =>
        _inner.EnableRemoteControlAsync(ct);

    public Task<RemoteControlStatusResult> DisableRemoteControlAsync(CancellationToken ct) =>
        _inner.DisableRemoteControlAsync(ct);

    public Task<RemoteControlStatusResult> ReadRemoteControlStatusAsync(CancellationToken ct) =>
        _inner.ReadRemoteControlStatusAsync(ct);
}
