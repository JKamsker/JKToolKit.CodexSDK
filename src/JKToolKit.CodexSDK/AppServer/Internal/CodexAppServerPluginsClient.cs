using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerPluginsClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerPluginsClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "plugin/list",
            new
            {
                cwds = options.Cwds,
                forceRemoteSync = options.ForceRemoteSync
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginListResult(result);
    }

    public async Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.MarketplacePath))
            throw new ArgumentException("MarketplacePath cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.PluginName))
            throw new ArgumentException("PluginName cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/read",
            new
            {
                marketplacePath = options.MarketplacePath,
                pluginName = options.PluginName
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginReadResult(result);
    }

    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.MarketplacePath))
            throw new ArgumentException("MarketplacePath cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.PluginName))
            throw new ArgumentException("PluginName cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/install",
            new
            {
                marketplacePath = options.MarketplacePath,
                pluginName = options.PluginName,
                forceRemoteSync = options.ForceRemoteSync
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginInstallResult(result);
    }

    public async Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.PluginId))
            throw new ArgumentException("PluginId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/uninstall",
            new
            {
                pluginId = options.PluginId,
                forceRemoteSync = options.ForceRemoteSync
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginUninstallResult(result);
    }
}
