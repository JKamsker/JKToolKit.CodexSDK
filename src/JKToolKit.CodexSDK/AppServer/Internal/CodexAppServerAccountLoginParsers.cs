using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerAccountLoginParsers
{
    public static object BuildStartParams(AccountLoginStartOptions options) =>
        options switch
        {
            AccountLoginStartOptions.ApiKey apiKey => new
            {
                type = "apiKey",
                apiKey = apiKey.ApiKeyValue
            },
            AccountLoginStartOptions.ChatGptBrowser => new
            {
                type = "chatgpt"
            },
            AccountLoginStartOptions.ChatGptDeviceCode => new
            {
                type = "chatgptDeviceCode"
            },
            AccountLoginStartOptions.ChatGptAuthTokens tokens => new
            {
                type = "chatgptAuthTokens",
                accessToken = tokens.AccessToken,
                chatgptAccountId = tokens.ChatGptAccountId,
                chatgptPlanType = tokens.ChatGptPlanType
            },
            _ => throw new ArgumentOutOfRangeException(nameof(options), options, "Unsupported account login start option type.")
        };

    public static AccountLoginStartResult ParseStartResult(JsonElement result)
    {
        var type = GetStringOrNull(result, "type");

        return type switch
        {
            "apiKey" => new AccountLoginStartResult.ApiKey
            {
                Raw = result
            },
            "chatgpt" => new AccountLoginStartResult.ChatGptBrowser
            {
                LoginId = GetRequiredString(result, "loginId", "account/login/start"),
                AuthUrl = GetRequiredString(result, "authUrl", "account/login/start"),
                Raw = result
            },
            "chatgptDeviceCode" => new AccountLoginStartResult.ChatGptDeviceCode
            {
                LoginId = GetRequiredString(result, "loginId", "account/login/start"),
                VerificationUrl = GetRequiredString(result, "verificationUrl", "account/login/start"),
                UserCode = GetRequiredString(result, "userCode", "account/login/start"),
                Raw = result
            },
            "chatgptAuthTokens" => new AccountLoginStartResult.ChatGptAuthTokens
            {
                Raw = result
            },
            _ => throw new InvalidOperationException(
                $"account/login/start returned an unknown login type '{type ?? "<missing>"}'. Raw result: {result}")
        };
    }

    public static AccountLoginCancelResult ParseCancelResult(JsonElement result) =>
        new()
        {
            Status = ParseCancelStatus(GetStringOrNull(result, "status")),
            Raw = result
        };

    private static AccountLoginCancelStatus ParseCancelStatus(string? value) =>
        value switch
        {
            "canceled" => AccountLoginCancelStatus.Canceled,
            "notFound" => AccountLoginCancelStatus.NotFound,
            _ => AccountLoginCancelStatus.Unknown
        };

    private static string GetRequiredString(JsonElement obj, string propertyName, string methodName)
    {
        var value = GetStringOrNull(obj, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"{methodName} response is missing required string '{propertyName}'. Raw result: {obj}");
    }
}
