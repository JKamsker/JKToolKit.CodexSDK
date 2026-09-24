using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

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

                var marketplacePath = CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.MarketplacePath);
                var message = CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.Message);
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
        var plugin = CodexAppServerClientJson.TryGetObject(result, JsonFieldNames.Plugin)
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

        var authPolicy = CodexAppServerClientJson.GetRequiredString(result, JsonFieldNames.AuthPolicy, "plugin/install response");

        return new PluginInstallResult
        {
            AppsNeedingAuth = apps,
            AuthPolicy = authPolicy,
            AuthPolicyValue = ParseRequiredPluginAuthPolicy(result, JsonFieldNames.AuthPolicy, "plugin/install response"),
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

    public static PluginSearchPage ParsePluginSearchPage(JsonElement result)
    {
        var dataArray = CodexAppServerClientJson.TryGetArray(result, JsonFieldNames.Data)
            ?? throw new InvalidOperationException("plugin/search returned no data array.");

        var data = new List<PluginSearchResult>();
        foreach (var item in dataArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/search data[] entries must be objects.");
            }

            var plugin = CodexAppServerClientJson.TryGetObject(item, JsonFieldNames.Plugin)
                ?? throw new InvalidOperationException("plugin/search data[] entries must contain a plugin object.");

            data.Add(new PluginSearchResult
            {
                Plugin = ParsePluginSummary(plugin),
                MarketplaceName = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.MarketplaceName, "plugin/search data[]"),
                MarketplacePath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                    CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.MarketplacePath),
                    "marketplacePath",
                    "plugin/search data[]"),
                Raw = item.Clone()
            });
        }

        return new PluginSearchPage
        {
            Data = data,
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, JsonFieldNames.NextCursor),
            Raw = result
        };
    }

    public static PluginReconcileResult ParsePluginReconcileResult(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("plugin/reconcile response must be a JSON object.");
        }

        var changedPluginsArray = CodexAppServerClientJson.TryGetArray(result, "changedPlugins")
            ?? throw new InvalidOperationException("plugin/reconcile returned no changedPlugins array.");
        var changedPlugins = new List<PluginReconcileChangedPlugin>();
        foreach (var item in changedPluginsArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/reconcile changedPlugins[] entries must be objects.");
            }

            changedPlugins.Add(new PluginReconcileChangedPlugin
            {
                Id = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.Id, "plugin/reconcile changedPlugins[]"),
                HasMcps = CodexAppServerClientJson.GetRequiredBool(item, "hasMcps", "plugin/reconcile changedPlugins[]"),
                HasApps = CodexAppServerClientJson.GetRequiredBool(item, "hasApps", "plugin/reconcile changedPlugins[]"),
                HasHooks = CodexAppServerClientJson.GetRequiredBool(item, "hasHooks", "plugin/reconcile changedPlugins[]"),
                HasSkills = CodexAppServerClientJson.GetRequiredBool(item, "hasSkills", "plugin/reconcile changedPlugins[]"),
                Raw = item.Clone()
            });
        }

        return new PluginReconcileResult
        {
            ChangedPlugins = changedPlugins,
            FailedRemotePluginIds = GetRequiredStringArray(result, "failedRemotePluginIds", "plugin/reconcile response"),
            FailedMaterializationRemotePluginIds = GetRequiredStringArray(
                result,
                "failedMaterializationRemotePluginIds",
                "plugin/reconcile response"),
            Raw = result
        };
    }

    internal static PluginSummaryDescriptor ParsePluginSummary(JsonElement item)
    {
        var availability = CodexAppServerClientJson.GetStringOrNull(item, "availability") ?? PluginAvailability.Available.Value;
        var installPolicySource = CodexAppServerClientJson.GetStringOrNull(item, "installPolicySource");
        var disabledReason = CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.DisabledReason);

        return new PluginSummaryDescriptor
        {
            Id = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.Id, "plugin summary"),
            Name = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.Name, "plugin summary"),
            RemotePluginId = CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.RemotePluginId),
            Version = CodexAppServerClientJson.GetStringOrNull(item, JsonFieldNames.Version),
            LocalVersion = CodexAppServerClientJson.GetStringOrNull(item, "localVersion"),
            Installed = CodexAppServerClientJson.GetRequiredBool(item, "installed", "plugin summary"),
            InstalledAt = GetUnixSecondsDateTimeOffset(item, "installedAt"),
            Enabled = CodexAppServerClientJson.GetRequiredBool(item, JsonFieldNames.Enabled, "plugin summary"),
            AuthPolicy = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.AuthPolicy, "plugin summary"),
            AuthPolicyValue = ParseRequiredPluginAuthPolicy(item, JsonFieldNames.AuthPolicy, "plugin summary"),
            InstallPolicy = CodexAppServerClientJson.GetRequiredString(item, JsonFieldNames.InstallPolicy, "plugin summary"),
            InstallPolicyValue = ParseRequiredPluginInstallPolicy(item, JsonFieldNames.InstallPolicy, "plugin summary"),
            InstallPolicySource = installPolicySource,
            InstallPolicySourceValue = PluginInstallPolicySource.TryParse(installPolicySource, out var parsedSource)
                ? (PluginInstallPolicySource?)parsedSource
                : null,
            Availability = availability,
            AvailabilityValue = PluginAvailability.Parse(availability),
            DisabledReason = disabledReason,
            DisabledReasonValue = PluginDisabledReason.TryParse(disabledReason, out var parsedDisabledReason)
                ? (PluginDisabledReason?)parsedDisabledReason
                : null,
            EligiblePlanTypes = CodexAppServerClientJson.GetOptionalStringArray(item, "eligiblePlanTypes"),
            ShareContext = CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(item),
            Keywords = CodexAppServerClientJson.GetOptionalStringArray(item, "keywords") ?? Array.Empty<string>(),
            Interface = ParsePluginInterface(item),
            Source = GetRequiredProperty(item, JsonFieldNames.Source, "plugin summary"),
            SourceInfo = ParseRequiredPluginSource(item, "plugin summary"),
            Raw = item.Clone()
        };
    }

    private static PluginMarketplaceInterfaceMetadata? ParsePluginMarketplaceInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, JsonFieldNames.Interface) is not { } interfaceObject)
        {
            return null;
        }

        return new PluginMarketplaceInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.DisplayName),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginInterfaceMetadata? ParsePluginInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, JsonFieldNames.Interface) is not { } interfaceObject)
        {
            return null;
        }

        var capabilities = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, JsonFieldNames.Capabilities) ?? Array.Empty<string>();
        var screenshots = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPaths(
            CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "screenshots"),
            "screenshots",
            "plugin interface");

        return new PluginInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.DisplayName),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.ShortDescription),
            LongDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "longDescription"),
            Category = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "category"),
            DeveloperName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "developerName"),
            BrandColor = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.BrandColor),
            DefaultPrompts = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, JsonFieldNames.DefaultPrompt) ?? Array.Empty<string>(),
            Capabilities = capabilities,
            Screenshots = screenshots,
            ScreenshotUrls = CodexAppServerClientJson.GetOptionalStringArray(interfaceObject, "screenshotUrls") ?? Array.Empty<string>(),
            PrivacyPolicyUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "privacyPolicyUrl"),
            TermsOfServiceUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "termsOfServiceUrl"),
            WebsiteUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.WebsiteUrl),
            ComposerIconPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.ComposerIcon),
                "composerIcon",
                "plugin interface"),
            ComposerIconUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "composerIconUrl"),
            ComposerIcon = ClonePropertyOrNull(interfaceObject, JsonFieldNames.ComposerIcon),
            LogoPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.Logo),
                "logo",
                "plugin interface"),
            LogoUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.LogoUrl),
            Logo = ClonePropertyOrNull(interfaceObject, JsonFieldNames.Logo),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginSkillInterfaceMetadata? ParsePluginSkillInterface(JsonElement item)
    {
        if (CodexAppServerClientJson.TryGetObject(item, JsonFieldNames.Interface) is not { } interfaceObject)
        {
            return null;
        }

        return new PluginSkillInterfaceMetadata
        {
            DisplayName = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.DisplayName),
            ShortDescription = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.ShortDescription),
            DefaultPrompt = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.DefaultPrompt),
            BrandColor = CodexAppServerClientJson.GetStringOrNull(interfaceObject, JsonFieldNames.BrandColor),
            IconSmall = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconSmall"),
            IconLarge = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconLarge"),
            IconSmallUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconSmallUrl"),
            IconLargeUrl = CodexAppServerClientJson.GetStringOrNull(interfaceObject, "iconLargeUrl"),
            Raw = interfaceObject.Clone()
        };
    }

    private static PluginSourceDescriptor ParseRequiredPluginSource(JsonElement item, string context)
    {
        if (CodexAppServerClientJson.TryGetObject(item, JsonFieldNames.Source) is not { } sourceObject)
        {
            throw new InvalidOperationException($"{context} is missing required object property 'source'.");
        }

        var sourceType = ParseRequiredPluginSourceType(sourceObject, JsonFieldNames.Type, "plugin source");
        var sourcePath = CodexAppServerClientJson.GetStringOrNull(sourceObject, JsonFieldNames.Path);
        var path = string.Equals(sourceType.Value, PluginSourceType.Local.Value, StringComparison.Ordinal)
            ? CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(sourcePath, "path", "plugin source")
            : sourcePath;

        return new PluginSourceDescriptor
        {
            Type = sourceType,
            Path = path,
            Url = CodexAppServerClientJson.GetStringOrNull(sourceObject, JsonFieldNames.Url),
            RefName = CodexAppServerClientJson.GetStringOrNull(sourceObject, "refName"),
            Sha = CodexAppServerClientJson.GetStringOrNull(sourceObject, JsonFieldNames.Sha),
            Raw = sourceObject.Clone()
        };
    }

    private static PluginAuthPolicy ParseRequiredPluginAuthPolicy(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginAuthPolicy>(item, propertyName, context, PluginAuthPolicy.TryParse);

    private static PluginInstallPolicy ParseRequiredPluginInstallPolicy(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginInstallPolicy>(item, propertyName, context, PluginInstallPolicy.TryParse);

    private static PluginSourceType ParseRequiredPluginSourceType(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginSourceType>(item, propertyName, context, PluginSourceType.TryParse);

    private static DateTimeOffset? GetUnixSecondsDateTimeOffset(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

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
