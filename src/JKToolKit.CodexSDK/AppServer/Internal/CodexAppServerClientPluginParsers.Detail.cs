using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static partial class CodexAppServerClientPluginParsers
{
    private static PluginMarketplace ParsePluginMarketplace(JsonElement item)
    {
        var plugins = new List<PluginSummaryDescriptor>();
        var pluginsArray = CodexAppServerClientJson.TryGetArray(item, "plugins")
            ?? throw new InvalidOperationException("plugin/list marketplaces[] entries must contain a plugins array.");
        foreach (var plugin in pluginsArray.EnumerateArray())
        {
            if (plugin.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/list marketplaces[].plugins[] entries must be objects.");
            }

            plugins.Add(ParsePluginSummary(plugin));
        }

        return new PluginMarketplace
        {
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin/list marketplaces[]"),
            Path = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
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
        var skillsArray = CodexAppServerClientJson.TryGetArray(item, "skills")
            ?? throw new InvalidOperationException("plugin/read returned a plugin without skills.");
        foreach (var skill in skillsArray.EnumerateArray())
        {
            if (skill.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/read skills[] entries must be objects.");
            }

            skills.Add(ParsePluginSkill(skill));
        }

        var apps = new List<PluginAppDescriptor>();
        var appsArray = CodexAppServerClientJson.TryGetArray(item, "apps")
            ?? throw new InvalidOperationException("plugin/read returned a plugin without apps.");
        foreach (var app in appsArray.EnumerateArray())
        {
            if (app.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/read apps[] entries must be objects.");
            }

            apps.Add(ParsePluginApp(app));
        }

        var mcpServers = GetRequiredStringArray(item, "mcpServers", "plugin/read plugin");
        var appTemplates = CodexAppServerClientPluginAppTemplateParsers.ParseAppTemplates(item);
        var hooks = ParsePluginHooks(item);

        return new PluginDetailDescriptor
        {
            Summary = ParsePluginSummary(summary),
            Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
            MarketplaceName = CodexAppServerClientJson.GetRequiredString(item, "marketplaceName", "plugin/read plugin"),
            MarketplacePath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(item, "marketplacePath"),
                "marketplacePath",
                "plugin/read plugin"),
            McpServers = mcpServers,
            Skills = skills,
            Apps = apps,
            AppTemplates = appTemplates,
            Hooks = hooks,
            Raw = item.Clone()
        };
    }

    private static IReadOnlyList<string> GetRequiredStringArray(JsonElement item, string propertyName, string context)
    {
        var array = CodexAppServerClientJson.TryGetArray(item, propertyName)
            ?? throw new InvalidOperationException($"{context} is missing required array property '{propertyName}'.");

        var values = new List<string>();
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"{context} property '{propertyName}' entries must be strings.");
            }

            values.Add(entry.GetString() ?? string.Empty);
        }

        return values;
    }

    private static PluginSkillDescriptor ParsePluginSkill(JsonElement item)
    {
        return new PluginSkillDescriptor
        {
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin skill"),
            Path = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(item, "path"),
                "path",
                "plugin skill"),
            Enabled = CodexAppServerClientJson.GetRequiredBool(item, "enabled", "plugin skill"),
            Description = CodexAppServerClientJson.GetRequiredString(item, "description", "plugin skill"),
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

    private static IReadOnlyList<PluginHookDescriptor> ParsePluginHooks(JsonElement item)
    {
        var hooksArray = CodexAppServerClientJson.TryGetArray(item, "hooks");
        if (hooksArray is null)
        {
            return Array.Empty<PluginHookDescriptor>();
        }

        var hooks = new List<PluginHookDescriptor>();
        foreach (var hook in hooksArray.Value.EnumerateArray())
        {
            if (hook.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/read hooks[] entries must be objects.");
            }

            hooks.Add(new PluginHookDescriptor
            {
                Key = CodexAppServerClientJson.GetRequiredString(hook, "key", "plugin hook"),
                EventName = CodexAppServerClientJson.GetRequiredString(hook, "eventName", "plugin hook"),
                Raw = hook.Clone()
            });
        }

        return hooks;
    }
}
