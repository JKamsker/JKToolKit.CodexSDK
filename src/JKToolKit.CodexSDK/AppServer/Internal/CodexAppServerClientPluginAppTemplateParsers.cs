using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerClientPluginAppTemplateParsers
{
    public static IReadOnlyList<PluginAppTemplateDescriptor> ParseAppTemplates(JsonElement plugin)
    {
        var templatesArray = CodexAppServerClientJson.TryGetArray(plugin, "appTemplates");
        if (templatesArray is null)
        {
            return Array.Empty<PluginAppTemplateDescriptor>();
        }

        var templates = new List<PluginAppTemplateDescriptor>();
        foreach (var item in templatesArray.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/read appTemplates[] entries must be objects.");
            }

            templates.Add(ParseAppTemplate(item));
        }

        return templates;
    }

    private static PluginAppTemplateDescriptor ParseAppTemplate(JsonElement item)
    {
        var reason = CodexAppServerClientJson.GetStringOrNull(item, "reason");
        var reasonValue = PluginAppTemplateUnavailableReason.TryParse(reason, out var parsedReason)
            ? parsedReason
            : (PluginAppTemplateUnavailableReason?)null;

        return new PluginAppTemplateDescriptor
        {
            TemplateId = CodexAppServerClientJson.GetRequiredString(item, "templateId", "plugin app template"),
            Name = CodexAppServerClientJson.GetRequiredString(item, "name", "plugin app template"),
            Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
            CanonicalConnectorId = CodexAppServerClientJson.GetStringOrNull(item, "canonicalConnectorId"),
            LogoUrl = CodexAppServerClientJson.GetStringOrNull(item, "logoUrl"),
            LogoUrlDark = CodexAppServerClientJson.GetStringOrNull(item, "logoUrlDark"),
            MaterializedAppIds = CodexAppServerClientJson.GetOptionalStringArray(item, "materializedAppIds") ?? Array.Empty<string>(),
            Reason = reason,
            ReasonValue = reasonValue,
            Raw = item.Clone()
        };
    }
}
