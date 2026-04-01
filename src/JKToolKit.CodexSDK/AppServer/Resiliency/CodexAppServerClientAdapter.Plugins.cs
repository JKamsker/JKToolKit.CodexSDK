namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct);

    Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct);

    Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct);

    Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct) => _inner.ListPluginsAsync(options, ct);

    public Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct) => _inner.ReadPluginAsync(options, ct);

    public Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct) => _inner.InstallPluginAsync(options, ct);

    public Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct) => _inner.UninstallPluginAsync(options, ct);
}
