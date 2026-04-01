#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.ListPluginsAsync(options, token), ct);

    public Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.ReadPluginAsync(options, token), ct);

    public Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.InstallPluginAsync(options, token), ct);

    public Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.UninstallPluginAsync(options, token), ct);
}

#pragma warning restore CS1591
