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
}
