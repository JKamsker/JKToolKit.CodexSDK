using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerClientPluginShareParsers
{
    public static PluginShareContextDescriptor? ParseShareContextOrNull(JsonElement owner)
    {
        if (CodexAppServerClientJson.TryGetObject(owner, "shareContext") is not { } context)
        {
            return null;
        }

        return new PluginShareContextDescriptor
        {
            RemotePluginId = CodexAppServerClientJson.GetRequiredString(context, "remotePluginId", "plugin share context"),
            RemoteVersion = CodexAppServerClientJson.GetStringOrNull(context, "remoteVersion"),
            Discoverability = ParseOptionalDiscoverability(context, "discoverability"),
            ShareUrl = CodexAppServerClientJson.GetStringOrNull(context, "shareUrl"),
            CreatorAccountUserId = CodexAppServerClientJson.GetStringOrNull(context, "creatorAccountUserId"),
            CreatorName = CodexAppServerClientJson.GetStringOrNull(context, "creatorName"),
            SharePrincipals = ParseOptionalPrincipals(context, "sharePrincipals"),
            Raw = context.Clone()
        };
    }

    public static PluginShareSaveResult ParseSaveResult(JsonElement result) =>
        new()
        {
            RemotePluginId = CodexAppServerClientJson.GetRequiredString(result, "remotePluginId", "plugin/share/save response"),
            ShareUrl = CodexAppServerClientJson.GetRequiredString(result, "shareUrl", "plugin/share/save response"),
            Raw = result
        };

    public static PluginShareUpdateTargetsResult ParseUpdateTargetsResult(JsonElement result) =>
        new()
        {
            Principals = ParseRequiredPrincipals(result, "principals", "plugin/share/updateTargets response"),
            Discoverability = ParseRequiredDiscoverability(result, "discoverability", "plugin/share/updateTargets response"),
            Raw = result
        };

    public static PluginShareListResult ParseListResult(JsonElement result)
    {
        var dataArray = CodexAppServerClientJson.TryGetArray(result, "data")
            ?? throw new InvalidOperationException("plugin/share/list returned no data array.");

        var items = new List<PluginShareListItem>();
        foreach (var item in dataArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("plugin/share/list data[] entries must be objects.");
            }

            var plugin = CodexAppServerClientJson.TryGetObject(item, "plugin")
                ?? throw new InvalidOperationException("plugin/share/list data[] entries must contain a plugin object.");

            items.Add(new PluginShareListItem
            {
                Plugin = CodexAppServerClientPluginParsers.ParsePluginSummary(plugin),
                LocalPluginPath = CodexAppServerPathValidation.GetOptionalAbsolutePayloadPath(
                    CodexAppServerClientJson.GetStringOrNull(item, "localPluginPath"),
                    "localPluginPath",
                    "plugin/share/list data[]"),
                Raw = item.Clone()
            });
        }

        return new PluginShareListResult
        {
            Data = items,
            Raw = result
        };
    }

    public static PluginShareCheckoutResult ParseCheckoutResult(JsonElement result) =>
        new()
        {
            RemotePluginId = CodexAppServerClientJson.GetRequiredString(result, "remotePluginId", "plugin/share/checkout response"),
            PluginId = CodexAppServerClientJson.GetRequiredString(result, "pluginId", "plugin/share/checkout response"),
            PluginName = CodexAppServerClientJson.GetRequiredString(result, "pluginName", "plugin/share/checkout response"),
            PluginPath = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(result, "pluginPath"),
                "pluginPath",
                "plugin/share/checkout response"),
            MarketplaceName = CodexAppServerClientJson.GetRequiredString(result, "marketplaceName", "plugin/share/checkout response"),
            MarketplacePath = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                CodexAppServerClientJson.GetStringOrNull(result, "marketplacePath"),
                "marketplacePath",
                "plugin/share/checkout response"),
            RemoteVersion = CodexAppServerClientJson.GetStringOrNull(result, "remoteVersion"),
            Raw = result
        };

    public static PluginShareDeleteResult ParseDeleteResult(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("plugin/share/delete response must be a JSON object.");
        }

        return new PluginShareDeleteResult { Raw = result };
    }

    public static object[]? BuildShareTargetsOrNull(IReadOnlyList<PluginShareTarget>? targets, string paramName) =>
        targets is null ? null : BuildShareTargets(targets, paramName);

    public static object[] BuildShareTargets(IReadOnlyList<PluginShareTarget>? targets, string paramName)
    {
        if (targets is null || targets.Count == 0)
        {
            return Array.Empty<object>();
        }

        var values = new object[targets.Count];
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i] ?? throw new ArgumentException($"ShareTargets[{i}] cannot be null.", paramName);
            ValidateWireValue(target.PrincipalType.Value, $"ShareTargets[{i}].PrincipalType", paramName);
            ValidateWireValue(target.PrincipalId, $"ShareTargets[{i}].PrincipalId", paramName);
            ValidateWireValue(target.Role.Value, $"ShareTargets[{i}].Role", paramName);

            values[i] = new
            {
                principalType = target.PrincipalType.Value,
                principalId = target.PrincipalId,
                role = target.Role.Value
            };
        }

        return values;
    }

    private static IReadOnlyList<PluginSharePrincipal>? ParseOptionalPrincipals(JsonElement item, string propertyName) =>
        CodexAppServerClientJson.TryGetArray(item, propertyName) is { } array
            ? ParsePrincipals(array, $"plugin share context {propertyName}")
            : null;

    private static IReadOnlyList<PluginSharePrincipal> ParseRequiredPrincipals(JsonElement item, string propertyName, string context)
    {
        var array = CodexAppServerClientJson.TryGetArray(item, propertyName)
            ?? throw new InvalidOperationException($"{context} is missing required array property '{propertyName}'.");

        return ParsePrincipals(array, $"{context} property '{propertyName}'");
    }

    private static IReadOnlyList<PluginSharePrincipal> ParsePrincipals(JsonElement array, string context)
    {
        var principals = new List<PluginSharePrincipal>();
        foreach (var principal in array.EnumerateArray())
        {
            if (principal.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"{context} entries must be objects.");
            }

            principals.Add(new PluginSharePrincipal
            {
                PrincipalType = ParseRequiredTypedValue<PluginSharePrincipalType>(
                    principal,
                    "principalType",
                    "plugin share principal",
                    PluginSharePrincipalType.TryParse),
                PrincipalId = CodexAppServerClientJson.GetRequiredString(principal, "principalId", "plugin share principal"),
                Role = ParseRequiredTypedValue<PluginSharePrincipalRole>(
                    principal,
                    "role",
                    "plugin share principal",
                    PluginSharePrincipalRole.TryParse),
                Name = CodexAppServerClientJson.GetRequiredString(principal, "name", "plugin share principal"),
                Raw = principal.Clone()
            });
        }

        return principals;
    }

    private static PluginShareDiscoverability? ParseOptionalDiscoverability(JsonElement item, string propertyName)
    {
        var value = CodexAppServerClientJson.GetStringOrNull(item, propertyName);
        return PluginShareDiscoverability.TryParse(value, out var parsed) ? parsed : default(PluginShareDiscoverability?);
    }

    private static PluginShareDiscoverability ParseRequiredDiscoverability(JsonElement item, string propertyName, string context) =>
        ParseRequiredTypedValue<PluginShareDiscoverability>(item, propertyName, context, PluginShareDiscoverability.TryParse);

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

    private static void ValidateWireValue(string? value, string displayName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
    }

    private delegate bool TryParseDelegate<T>(string? value, out T parsed);
}
