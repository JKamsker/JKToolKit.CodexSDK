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
        CodexAppServerPathValidation.ValidateOptionalAbsolutePaths(options.Cwds, nameof(options), "Cwds");

        var result = await _sendRequestAsync(
            "plugin/list",
            new
            {
                cwds = options.Cwds,
                marketplaceKinds = BuildMarketplaceKinds(options.MarketplaceKinds, nameof(options))
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginListResult(result);
    }

    public async Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateMarketplaceSelector(options.MarketplacePath, options.RemoteMarketplaceName, nameof(options));
        if (string.IsNullOrWhiteSpace(options.PluginName))
            throw new ArgumentException("PluginName cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/read",
            new
            {
                marketplacePath = options.MarketplacePath,
                remoteMarketplaceName = options.RemoteMarketplaceName,
                pluginName = options.PluginName
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginReadResult(result);
    }

    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateMarketplaceSelector(options.MarketplacePath, options.RemoteMarketplaceName, nameof(options));
        if (string.IsNullOrWhiteSpace(options.PluginName))
            throw new ArgumentException("PluginName cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/install",
            new
            {
                marketplacePath = options.MarketplacePath,
                remoteMarketplaceName = options.RemoteMarketplaceName,
                pluginName = options.PluginName
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
                pluginId = options.PluginId
            },
            ct);

        return CodexAppServerClientPluginParsers.ParsePluginUninstallResult(result);
    }

    public async Task<PluginShareSaveResult> SavePluginShareAsync(PluginShareSaveOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        CodexAppServerPathValidation.ValidateRequiredAbsolutePath(options.PluginPath, nameof(options), "PluginPath");
        ValidateOptionalWireValue(options.RemotePluginId, "RemotePluginId", nameof(options));
        ValidateOptionalWireValue(options.Discoverability?.Value, "Discoverability", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/share/save",
            new
            {
                pluginPath = options.PluginPath,
                remotePluginId = options.RemotePluginId,
                discoverability = options.Discoverability?.Value,
                shareTargets = CodexAppServerClientPluginShareParsers.BuildShareTargetsOrNull(options.ShareTargets, nameof(options))
            },
            ct);

        return CodexAppServerClientPluginShareParsers.ParseSaveResult(result);
    }

    public async Task<PluginShareUpdateTargetsResult> UpdatePluginShareTargetsAsync(
        PluginShareUpdateTargetsOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.RemotePluginId, "RemotePluginId", nameof(options));
        ValidateRequiredWireValue(options.Discoverability.Value, "Discoverability", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/share/updateTargets",
            new
            {
                remotePluginId = options.RemotePluginId,
                discoverability = options.Discoverability.Value,
                shareTargets = CodexAppServerClientPluginShareParsers.BuildShareTargets(options.ShareTargets, nameof(options))
            },
            ct);

        return CodexAppServerClientPluginShareParsers.ParseUpdateTargetsResult(result);
    }

    public async Task<PluginShareListResult> ListPluginSharesAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync("plugin/share/list", new { }, ct);
        return CodexAppServerClientPluginShareParsers.ParseListResult(result);
    }

    public async Task<PluginShareCheckoutResult> CheckoutPluginShareAsync(PluginShareCheckoutOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.RemotePluginId, "RemotePluginId", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/share/checkout",
            new { remotePluginId = options.RemotePluginId },
            ct);

        return CodexAppServerClientPluginShareParsers.ParseCheckoutResult(result);
    }

    public async Task<PluginShareDeleteResult> DeletePluginShareAsync(PluginShareDeleteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.RemotePluginId, "RemotePluginId", nameof(options));

        var result = await _sendRequestAsync(
            "plugin/share/delete",
            new { remotePluginId = options.RemotePluginId },
            ct);

        return CodexAppServerClientPluginShareParsers.ParseDeleteResult(result);
    }

    private static string[]? BuildMarketplaceKinds(IReadOnlyList<PluginListMarketplaceKind>? kinds, string paramName)
    {
        if (kinds is null)
        {
            return null;
        }

        var values = new string[kinds.Count];
        for (var i = 0; i < kinds.Count; i++)
        {
            ValidateRequiredWireValue(kinds[i].Value, $"MarketplaceKinds[{i}]", paramName);
            values[i] = kinds[i].Value;
        }

        return values;
    }

    private static void ValidateMarketplaceSelector(string? marketplacePath, string? remoteMarketplaceName, string paramName)
    {
        var hasMarketplacePath = !string.IsNullOrWhiteSpace(marketplacePath);
        var hasRemoteMarketplaceName = !string.IsNullOrWhiteSpace(remoteMarketplaceName);
        if (hasMarketplacePath == hasRemoteMarketplaceName)
        {
            throw new ArgumentException("Exactly one of MarketplacePath or RemoteMarketplaceName must be provided.", paramName);
        }

        if (hasMarketplacePath)
        {
            CodexAppServerPathValidation.ValidateRequiredAbsolutePath(marketplacePath, paramName, "MarketplacePath");
        }
    }

    private static void ValidateOptionalWireValue(string? value, string displayName, string paramName)
    {
        if (value is null)
        {
            return;
        }

        ValidateRequiredWireValue(value, displayName, paramName);
    }

    private static void ValidateRequiredWireValue(string? value, string displayName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
    }
}
