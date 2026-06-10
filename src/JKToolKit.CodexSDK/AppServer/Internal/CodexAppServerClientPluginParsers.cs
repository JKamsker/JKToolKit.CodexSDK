using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static partial class CodexAppServerClientPluginParsers
{
    public static PluginListResult ParsePluginListResult(JsonElement result)
    {
        var marketplaces = new List<PluginMarketplace>();
        var marketplacesArray = CodexAppServerClientJson.TryGetArray(result, "marketplaces")
            ?? throw new InvalidOperationException("plugin/list returned no marketplaces array.");
        foreach (var item in marketplacesArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/list marketplaces[] entries must be objects.");
            }

            marketplaces.Add(ParsePluginMarketplace(item));
        }

        var errors = new List<MarketplaceLoadError>();
        if (CodexAppServerClientJson.TryGetArray(result, "marketplaceLoadErrors") is { } errorsArray)
        {
            foreach (var item in errorsArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("plugin/list marketplaceLoadErrors[] entries must be objects.");
                }

                var marketplacePath = CodexAppServerClientJson.GetStringOrNull(item, "marketplacePath");
                var message = CodexAppServerClientJson.GetStringOrNull(item, "message");
                if (string.IsNullOrWhiteSpace(marketplacePath) || string.IsNullOrWhiteSpace(message))
                {
                    throw new InvalidOperationException("plugin/list marketplaceLoadErrors[] entries must contain marketplacePath and message.");
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

    public static PluginUninstallResult ParsePluginUninstallResult(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("plugin/uninstall response must be a JSON object.");
        }

        return new PluginUninstallResult
        {
            Raw = result
        };
    }

    internal static PluginSummaryDescriptor ParsePluginSummary(JsonElement item)
    {
        var availability = CodexAppServerClientJson.GetStringOrNull(item, "availability") ?? PluginAvailability.Available.Value;

        return new PluginSummaryDescriptor
        {
            Id = CodexAppServerClientJson.GetRequiredString(item, "id", "plugin summary"),
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin summary"),
            RemotePluginId = CodexAppServerClientJson.GetStringOrNull(item, "remotePluginId"),
            LocalVersion = CodexAppServerClientJson.GetStringOrNull(item, "localVersion"),
            Installed = CodexAppServerClientJson.GetRequiredBool(item, "installed", "plugin summary"),
            Enabled = CodexAppServerClientJson.GetRequiredBool(item, "enabled", "plugin summary"),
            AuthPolicy = CodexAppServerClientJson.GetRequiredString(item, "authPolicy", "plugin summary"),
            AuthPolicyValue = ParseRequiredPluginAuthPolicy(item, "authPolicy", "plugin summary"),
            InstallPolicy = CodexAppServerClientJson.GetRequiredString(item, "installPolicy", "plugin summary"),
            InstallPolicyValue = ParseRequiredPluginInstallPolicy(item, "installPolicy", "plugin summary"),
            Availability = availability,
            AvailabilityValue = PluginAvailability.Parse(availability),
            ShareContext = CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(item),
            Keywords = CodexAppServerClientJson.GetOptionalStringArray(item, "keywords") ?? Array.Empty<string>(),
            Interface = ParsePluginInterface(item),
            Source = GetRequiredProperty(item, "source", "plugin summary"),
            SourceInfo = ParseRequiredPluginSource(item, "plugin summary"),
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

        var capabilities = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "capabilities") ?? Array.Empty<string>();
        var screenshots = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPaths(
            CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "screenshots"),
            "screenshots",
            "plugin interface");

        return new PluginInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "displayName"),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "shortDescription"),
            LongDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "longDescription"),
            Category = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "category"),
            DeveloperName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "developerName"),
            BrandColor = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "brandColor"),
            DefaultPrompts = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "defaultPrompt") ?? Array.Empty<string>(),
            Capabilities = capabilities,
            Screenshots = screenshots,
            ScreenshotUrls = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "screenshotUrls") ?? Array.Empty<string>(),
            PrivacyPolicyUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "privacyPolicyUrl"),
            TermsOfServiceUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "termsOfServiceUrl"),
            WebsiteUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "websiteUrl"),
            ComposerIconPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, "composerIcon"),
                "composerIcon",
                "plugin interface"),
            ComposerIconUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "composerIconUrl"),
            ComposerIcon = ClonePropertyOrNull(interfaceObject, "composerIcon"),
            LogoPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, "logo"),
                "logo",
                "plugin interface"),
            LogoUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "logoUrl"),
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

        var sourceType = ParseRequiredPluginSourceType(sourceObject, "type", "plugin source");
        var sourcePath = CodexAppServerClientJson.GetStringOrNull(sourceObject, "path");
        var path = string.Equals(sourceType.Value, PluginSourceType.Local.Value, StringComparison.Ordinal)
            ? CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(sourcePath, "path", "plugin source")
            : sourcePath;

        return new PluginSourceDescriptor
        {
            Type = sourceType,
            Path = path,
            Url = CodexAppServerClientJson.GetStringOrNull(sourceObject, "url"),
            RefName = CodexAppServerClientJson.GetStringOrNull(sourceObject, "refName"),
            Sha = CodexAppServerClientJson.GetStringOrNull(sourceObject, "sha"),
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
