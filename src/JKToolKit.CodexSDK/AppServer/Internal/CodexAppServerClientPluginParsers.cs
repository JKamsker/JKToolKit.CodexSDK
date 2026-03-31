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
                    MarketplacePath = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                        marketplacePath,
                        "marketplacePath",
                        "plugin/list marketplaceLoadErrors[]"),
                    Message = message
                });
            }
        }

        return new PluginListResult
        {
            Marketplaces = marketplaces,
            FeaturedPluginIds = CodexAppServerClientJson.GetOptionalStringArray(result, "featuredPluginIds") ?? Array.Empty<string>(),
            MarketplaceLoadErrors = errors,
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
            AuthPolicyValue = ParsePluginAuthPolicy(result, "authPolicy"),
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
            Path = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(item, "path"),
                "path",
                "plugin/list marketplaces[]"),
            Interface = ParsePluginMarketplaceInterface(item),
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
            MarketplacePath = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(item, "marketplacePath"),
                "marketplacePath",
                "plugin/read plugin"),
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
            AuthPolicyValue = ParsePluginAuthPolicy(item, "authPolicy"),
            InstallPolicy = CodexAppServerClientJson.GetStringOrNull(item, "installPolicy"),
            InstallPolicyValue = ParsePluginInstallPolicy(item, "installPolicy"),
            Interface = ParsePluginInterface(item),
            Source = ClonePropertyOrNull(item, "source"),
            SourceInfo = ParsePluginSource(item),
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
            Interface = ParsePluginSkillInterface(item),
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

    private static PluginMarketplaceInterfaceMetadata? ParsePluginMarketplaceInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, "interface") is not { } interfaceObject)
        {
            return null;
        }

        return new PluginMarketplaceInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "displayName"),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginInterfaceMetadata? ParsePluginInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, "interface") is not { } interfaceObject)
        {
            return null;
        }

        return new PluginInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "displayName"),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "shortDescription"),
            LongDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "longDescription"),
            Category = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "category"),
            DeveloperName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "developerName"),
            BrandColor = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "brandColor"),
            DefaultPrompts = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "defaultPrompt") ?? Array.Empty<string>(),
            Capabilities = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "capabilities") ?? Array.Empty<string>(),
            Screenshots = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPaths(
                CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "screenshots"),
                "screenshots",
                "plugin interface"),
            PrivacyPolicyUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "privacyPolicyUrl"),
            TermsOfServiceUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "termsOfServiceUrl"),
            WebsiteUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "websiteUrl"),
            ComposerIconPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, "composerIcon"),
                "composerIcon",
                "plugin interface"),
            ComposerIcon = ClonePropertyOrNull(interfaceObject, "composerIcon"),
            LogoPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, "logo"),
                "logo",
                "plugin interface"),
            Logo = ClonePropertyOrNull(interfaceObject, "logo"),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginSkillInterfaceMetadata? ParsePluginSkillInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, "interface") is not { } interfaceObject)
        {
            return null;
        }

        return new PluginSkillInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "displayName"),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "shortDescription"),
            DefaultPrompt = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "defaultPrompt"),
            BrandColor = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "brandColor"),
            IconSmall = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconSmall"),
            IconLarge = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconLarge"),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginSourceDescriptor? ParsePluginSource(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, "source") is not { } sourceObject)
        {
            return null;
        }

        return new PluginSourceDescriptor
        {
            Type = ParsePluginSourceType(sourceObject, "type"),
            Path = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(sourceObject, "path"),
                "path",
                "plugin source"),
            Raw = sourceObject.Clone()
        };
    }

    private static PluginAuthPolicy? ParsePluginAuthPolicy(JsonElement item, string propertyName) =>
        ParseTypedValue<PluginAuthPolicy>(item, propertyName, PluginAuthPolicy.TryParse);

    private static PluginInstallPolicy? ParsePluginInstallPolicy(JsonElement item, string propertyName) =>
        ParseTypedValue<PluginInstallPolicy>(item, propertyName, PluginInstallPolicy.TryParse);

    private static PluginSourceType? ParsePluginSourceType(JsonElement item, string propertyName) =>
        ParseTypedValue<PluginSourceType>(item, propertyName, PluginSourceType.TryParse);

    private static T? ParseTypedValue<T>(JsonElement item, string propertyName, TryParseDelegate<T> tryParse)
        where T : struct
    {
        var value = CodexAppServerClientJson.GetStringOrNull(item, propertyName);
        return tryParse(value, out var parsed) ? parsed : null;
    }

    private static JsonElement? ClonePropertyOrNull(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : property.Clone();
    }

    private delegate bool TryParseDelegate<T>(string? value, out T parsed);
}
