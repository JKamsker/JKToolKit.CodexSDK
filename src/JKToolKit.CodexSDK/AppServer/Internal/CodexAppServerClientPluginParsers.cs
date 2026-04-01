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
        var appsArray = CodexAppServerClientJson.TryGetArray(result, "appsNeedingAuth")
            ?? throw new InvalidOperationException("plugin/install returned no appsNeedingAuth array.");

        var apps = new List<PluginAppDescriptor>();
        foreach (var item in appsArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/install appsNeedingAuth[] entries must be objects.");
            }

            apps.Add(ParsePluginApp(item));
        }

        var authPolicy = CodexAppServerClientJson.GetRequiredString(result, "authPolicy", "plugin/install response");

        return new PluginInstallResult
        {
            AppsNeedingAuth = apps,
            AuthPolicy = authPolicy,
            AuthPolicyValue = ParseRequiredPluginAuthPolicy(result, "authPolicy", "plugin/install response"),
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
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin/list marketplaces[]"),
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
            MarketplaceName = CodexAppServerClientJson.GetRequiredString(item, "marketplaceName", "plugin/read plugin"),
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
            Id = CodexAppServerClientJson.GetRequiredString(item, "id", "plugin summary"),
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin summary"),
            Installed = CodexAppServerClientJson.GetRequiredBool(item, "installed", "plugin summary"),
            Enabled = CodexAppServerClientJson.GetRequiredBool(item, "enabled", "plugin summary"),
            AuthPolicy = CodexAppServerClientJson.GetRequiredString(item, "authPolicy", "plugin summary"),
            AuthPolicyValue = ParseRequiredPluginAuthPolicy(item, "authPolicy", "plugin summary"),
            InstallPolicy = CodexAppServerClientJson.GetRequiredString(item, "installPolicy", "plugin summary"),
            InstallPolicyValue = ParseRequiredPluginInstallPolicy(item, "installPolicy", "plugin summary"),
            Interface = ParsePluginInterface(item),
            Source = GetRequiredProperty(item, "source", "plugin summary"),
            SourceInfo = ParseRequiredPluginSource(item, "plugin summary"),
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
            Id = CodexAppServerClientJson.GetRequiredString(item, "id", "plugin app"),
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin app"),
            NeedsAuth = CodexAppServerClientJson.GetRequiredBool(item, "needsAuth", "plugin app"),
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

    private static PluginSourceDescriptor ParseRequiredPluginSource(JsonElement item, string context)
    {
        if (CodexAppServerClientJson.TryGetObject(item, "source") is not { } sourceObject)
        {
            throw new InvalidOperationException($"{context} is missing required object property 'source'.");
        }

        return new PluginSourceDescriptor
        {
            Type = ParseRequiredPluginSourceType(sourceObject, "type", "plugin source"),
            Path = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(sourceObject, "path"),
                "path",
                "plugin source"),
            Raw = sourceObject.Clone()
        };
    }

    private static PluginAuthPolicy ParseRequiredPluginAuthPolicy(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginAuthPolicy>(item, propertyName, context, PluginAuthPolicy.TryParse);

    private static PluginInstallPolicy ParseRequiredPluginInstallPolicy(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginInstallPolicy>(item, propertyName, context, PluginInstallPolicy.TryParse);

    private static PluginSourceType ParseRequiredPluginSourceType(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginSourceType>(item, propertyName, context, PluginSourceType.TryParse);

    private static T ParseRequiredTypedValue<T>(JsonElement item, string propertyName, string context, TryParseDelegate<T> tryParse)
        where T : struct
    {
        var value = CodexAppServerClientJson.GetStringOrNull(item, propertyName);
        if (tryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"{context} property '{propertyName}' is missing or invalid.");
    }

    private static JsonElement GetRequiredProperty(JsonElement item, string propertyName, string context)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            throw new InvalidOperationException($"{context} is missing required property '{propertyName}'.");
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"{context} property '{propertyName}' cannot be null.");
        }

        return property.Clone();
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
