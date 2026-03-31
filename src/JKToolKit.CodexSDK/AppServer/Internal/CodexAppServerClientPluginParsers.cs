using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerClientPluginParsers
{
    public static PluginListResult ParsePluginListResult(JsonElement result)
    {
        var marketplaces = new List<PluginMarketplace>();
        if (CodexAppServerClientJson.TryGetArray(result, "marketplaces") is { } marketplacesArray)
        {
            foreach (var item in marketplacesArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    marketplaces.Add(ParsePluginMarketplace(item));
                }
            }
        }

        var errors = new List<MarketplaceLoadError>();
        if (CodexAppServerClientJson.TryGetArray(result, "marketplaceLoadErrors") is { } errorsArray)
        {
            foreach (var item in errorsArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var marketplacePath = CodexAppServerClientJson.GetStringOrNull(item, "marketplacePath");
                var message = CodexAppServerClientJson.GetStringOrNull(item, "message");
                if (string.IsNullOrWhiteSpace(marketplacePath) || string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                errors.Add(new MarketplaceLoadError
                {
                    MarketplacePath = marketplacePath,
                    Message = message
                });
            }
        }

        return new PluginListResult
        {
            Marketplaces = marketplaces,
            FeaturedPluginIds = CodexAppServerClientJson.GetOptionalStringArray(result, "featuredPluginIds"),
            MarketplaceLoadErrors = errors.Count == 0 ? null : errors,
            RemoteSyncError = CodexAppServerClientJson.GetStringOrNull(result, "remoteSyncError"),
            Raw = result
        };
    }

    public static PluginReadResult ParsePluginReadResult(JsonElement result)
    {
        var plugin = CodexAppServerClientJson.TryGetObject(result, "plugin")
            ?? throw new InvalidOperationException("plugin/read returned no plugin object.");

        return new PluginReadResult
        {
            Plugin = ParsePluginDetail(plugin),
            Raw = result
        };
    }

    public static PluginInstallResult ParsePluginInstallResult(JsonElement result)
    {
        var apps = new List<PluginAppDescriptor>();
        if (CodexAppServerClientJson.TryGetArray(result, "appsNeedingAuth") is { } appsArray)
        {
            foreach (var item in appsArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    apps.Add(ParsePluginApp(item));
                }
            }
        }

        return new PluginInstallResult
        {
            AppsNeedingAuth = apps,
            AuthPolicy = CodexAppServerClientJson.GetStringOrNull(result, "authPolicy"),
            Raw = result
        };
    }

    public static PluginUninstallResult ParsePluginUninstallResult(JsonElement result) =>
        new()
        {
            Raw = result
        };

    private static PluginMarketplace ParsePluginMarketplace(JsonElement item)
    {
        var plugins = new List<PluginSummaryDescriptor>();
        if (CodexAppServerClientJson.TryGetArray(item, "plugins") is { } pluginsArray)
        {
            foreach (var plugin in pluginsArray.EnumerateArray())
            {
                if (plugin.ValueKind == JsonValueKind.Object)
                {
                    plugins.Add(ParsePluginSummary(plugin));
                }
            }
        }

        return new PluginMarketplace
        {
            Name = CodexAppServerClientJson.GetStringOrNull(item, "name") ?? string.Empty,
            Path = CodexAppServerClientJson.GetStringOrNull(item, "path") ?? string.Empty,
            Plugins = plugins,
            Raw = item.Clone()
        };
    }

    private static PluginDetailDescriptor ParsePluginDetail(JsonElement item)
    {
        var summary = CodexAppServerClientJson.TryGetObject(item, "summary")
            ?? throw new InvalidOperationException("plugin/read returned a plugin without summary.");

        var skills = new List<PluginSkillDescriptor>();
        if (CodexAppServerClientJson.TryGetArray(item, "skills") is { } skillsArray)
        {
            foreach (var skill in skillsArray.EnumerateArray())
            {
                if (skill.ValueKind == JsonValueKind.Object)
                {
                    skills.Add(ParsePluginSkill(skill));
                }
            }
        }

        var apps = new List<PluginAppDescriptor>();
        if (CodexAppServerClientJson.TryGetArray(item, "apps") is { } appsArray)
        {
            foreach (var app in appsArray.EnumerateArray())
            {
                if (app.ValueKind == JsonValueKind.Object)
                {
                    apps.Add(ParsePluginApp(app));
                }
            }
        }

        return new PluginDetailDescriptor
        {
            Summary = ParsePluginSummary(summary),
            Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
            MarketplaceName = CodexAppServerClientJson.GetStringOrNull(item, "marketplaceName") ?? string.Empty,
            MarketplacePath = CodexAppServerClientJson.GetStringOrNull(item, "marketplacePath") ?? string.Empty,
            McpServers = CodexAppServerClientJson.GetOptionalStringArray(item, "mcpServers") ?? Array.Empty<string>(),
            Skills = skills,
            Apps = apps,
            Raw = item.Clone()
        };
    }

    private static PluginSummaryDescriptor ParsePluginSummary(JsonElement item)
    {
        return new PluginSummaryDescriptor
        {
            Id = CodexAppServerClientJson.GetStringOrNull(item, "id") ?? string.Empty,
            Name = CodexAppServerClientJson.GetStringOrNull(item, "name") ?? string.Empty,
            Installed = CodexAppServerClientJson.GetBoolOrNull(item, "installed") == true,
            Enabled = CodexAppServerClientJson.GetBoolOrNull(item, "enabled") == true,
            AuthPolicy = CodexAppServerClientJson.GetStringOrNull(item, "authPolicy"),
            InstallPolicy = CodexAppServerClientJson.GetStringOrNull(item, "installPolicy"),
            Source = item.TryGetProperty("source", out var source) && source.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? source.Clone()
                : null,
            Raw = item.Clone()
        };
    }

    private static PluginSkillDescriptor ParsePluginSkill(JsonElement item)
    {
        return new PluginSkillDescriptor
        {
            Name = CodexAppServerClientJson.GetStringOrNull(item, "name") ?? string.Empty,
            Path = CodexAppServerClientJson.GetStringOrNull(item, "path") ?? string.Empty,
            Enabled = CodexAppServerClientJson.GetBoolOrNull(item, "enabled") == true,
            Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(item, "shortDescription"),
            Raw = item.Clone()
        };
    }

    private static PluginAppDescriptor ParsePluginApp(JsonElement item)
    {
        return new PluginAppDescriptor
        {
            Id = CodexAppServerClientJson.GetStringOrNull(item, "id") ?? string.Empty,
            Name = CodexAppServerClientJson.GetStringOrNull(item, "name") ?? string.Empty,
            NeedsAuth = CodexAppServerClientJson.GetBoolOrNull(item, "needsAuth") == true,
            Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
            InstallUrl = CodexAppServerClientJson.GetStringOrNull(item, "installUrl"),
            Raw = item.Clone()
        };
    }
}
