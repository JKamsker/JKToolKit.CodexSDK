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

    public Task<PluginShareSaveResult> SavePluginShareAsync(PluginShareSaveOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.SavePluginShareAsync(options, token), ct);

    public Task<PluginShareUpdateTargetsResult> UpdatePluginShareTargetsAsync(
        PluginShareUpdateTargetsOptions options,
        CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.UpdatePluginShareTargetsAsync(options, token), ct);

    public Task<PluginShareListResult> ListPluginSharesAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.ListPluginSharesAsync(token), ct);

    public Task<PluginShareCheckoutResult> CheckoutPluginShareAsync(
        PluginShareCheckoutOptions options,
        CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.CheckoutPluginShareAsync(options, token), ct);

    public Task<PluginShareDeleteResult> DeletePluginShareAsync(
        PluginShareDeleteOptions options,
        CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Plugins, (c, token) => c.DeletePluginShareAsync(options, token), ct);
}

#pragma warning restore CS1591
