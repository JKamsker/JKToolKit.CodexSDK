namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct);

    Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct);

    Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct);

    Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct);

    Task<PluginShareSaveResult> SavePluginShareAsync(PluginShareSaveOptions options, CancellationToken ct);

    Task<PluginShareUpdateTargetsResult> UpdatePluginShareTargetsAsync(PluginShareUpdateTargetsOptions options, CancellationToken ct);

    Task<PluginShareListResult> ListPluginSharesAsync(CancellationToken ct);

    Task<PluginShareCheckoutResult> CheckoutPluginShareAsync(PluginShareCheckoutOptions options, CancellationToken ct);

    Task<PluginShareDeleteResult> DeletePluginShareAsync(PluginShareDeleteOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct) => _inner.ListPluginsAsync(options, ct);

    public Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct) => _inner.ReadPluginAsync(options, ct);

    public Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct) => _inner.InstallPluginAsync(options, ct);

    public Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct) => _inner.UninstallPluginAsync(options, ct);

    public Task<PluginShareSaveResult> SavePluginShareAsync(PluginShareSaveOptions options, CancellationToken ct) =>
        _inner.SavePluginShareAsync(options, ct);

    public Task<PluginShareUpdateTargetsResult> UpdatePluginShareTargetsAsync(
        PluginShareUpdateTargetsOptions options,
        CancellationToken ct) =>
        _inner.UpdatePluginShareTargetsAsync(options, ct);

    public Task<PluginShareListResult> ListPluginSharesAsync(CancellationToken ct) => _inner.ListPluginSharesAsync(ct);

    public Task<PluginShareCheckoutResult> CheckoutPluginShareAsync(PluginShareCheckoutOptions options, CancellationToken ct) =>
        _inner.CheckoutPluginShareAsync(options, ct);

    public Task<PluginShareDeleteResult> DeletePluginShareAsync(PluginShareDeleteOptions options, CancellationToken ct) =>
        _inner.DeletePluginShareAsync(options, ct);
}
