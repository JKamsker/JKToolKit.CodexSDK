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
}
