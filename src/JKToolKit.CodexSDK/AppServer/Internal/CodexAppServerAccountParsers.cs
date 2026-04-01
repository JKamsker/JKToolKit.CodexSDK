using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerAccountParsers
{
    public static CodexAccountInfo? ParseAccountOrNull(JsonElement obj, string propertyName, string context)
    {
        var account = TryGetObject(obj, propertyName);
        return account.HasValue ? ParseAccount(account.Value, context) : null;
    }

    public static CodexAccountInfo ParseAccount(JsonElement account, string context)
    {
        var type = GetRequiredString(account, "type", context);
        return type switch
        {
            "apiKey" => new CodexApiKeyAccountInfo(account.Clone()),
            "chatgpt" => new CodexChatGptAccountInfo(
                Email: GetRequiredString(account, "email", context),
                PlanType: CodexPlanType.Parse(GetRequiredString(account, "planType", context)),
                Raw: account.Clone()),
            _ => throw new InvalidOperationException(
                $"{context} returned unsupported account type '{type}'. Raw result: {account}")
        };
    }

    public static CodexAuthMode? ParseAuthModeOrNull(JsonElement obj, string propertyName, string context) =>
        ParseOptionalValue(obj, propertyName, context, CodexAuthMode.Parse);

    public static CodexPlanType? ParsePlanTypeOrNull(JsonElement obj, string propertyName, string context) =>
        ParseOptionalValue(obj, propertyName, context, CodexPlanType.Parse);

    private static T? ParseOptionalValue<T>(JsonElement obj, string propertyName, string context, Func<string, T> parse)
        where T : struct
    {
        var value = TryGetElement(obj, propertyName);
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"{context} property '{propertyName}' must be a string or null.");
        }

        var raw = value.Value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException($"{context} property '{propertyName}' cannot be empty or whitespace.");
        }

        return parse(raw);
    }
}
