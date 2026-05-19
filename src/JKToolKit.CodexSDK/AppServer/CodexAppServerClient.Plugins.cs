namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Lists available plugin marketplaces and plugins.
    /// </summary>
    public Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct = default) =>
        _pluginsClient.ListPluginsAsync(options, ct);

    /// <summary>
    /// Reads plugin details.
    /// </summary>
    public Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct = default) =>
        _pluginsClient.ReadPluginAsync(options, ct);

    /// <summary>
    /// Installs a plugin.
    /// </summary>
    public Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct = default) =>
        _pluginsClient.InstallPluginAsync(options, ct);

    /// <summary>
    /// Uninstalls a plugin.
    /// </summary>
    public Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct = default) =>
        _pluginsClient.UninstallPluginAsync(options, ct);

    /// <summary>
    /// Saves a local plugin as a remote shared plugin.
    /// </summary>
    public Task<PluginShareSaveResult> SavePluginShareAsync(PluginShareSaveOptions options, CancellationToken ct = default) =>
        _pluginsClient.SavePluginShareAsync(options, ct);

    /// <summary>
    /// Updates remote plugin share targets.
    /// </summary>
    public Task<PluginShareUpdateTargetsResult> UpdatePluginShareTargetsAsync(
        PluginShareUpdateTargetsOptions options,
        CancellationToken ct = default) =>
        _pluginsClient.UpdatePluginShareTargetsAsync(options, ct);

    /// <summary>
    /// Lists remote plugin shares visible to the current account.
    /// </summary>
    public Task<PluginShareListResult> ListPluginSharesAsync(CancellationToken ct = default) =>
        _pluginsClient.ListPluginSharesAsync(ct);

    /// <summary>
    /// Checks out a remote shared plugin into a local marketplace.
    /// </summary>
    public Task<PluginShareCheckoutResult> CheckoutPluginShareAsync(PluginShareCheckoutOptions options, CancellationToken ct = default) =>
        _pluginsClient.CheckoutPluginShareAsync(options, ct);

    /// <summary>
    /// Deletes a remote shared plugin.
    /// </summary>
    public Task<PluginShareDeleteResult> DeletePluginShareAsync(PluginShareDeleteOptions options, CancellationToken ct = default) =>
        _pluginsClient.DeletePluginShareAsync(options, ct);
}
