using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;

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
        var type = GetStringOrNull(result, JsonFieldNames.Type);

        return type switch
        {
            "apiKey" => new AccountLoginStartResult.ApiKey
            {
                Raw = result
            },
            "chatgpt" => new AccountLoginStartResult.ChatGptBrowser
            {
                LoginId = GetRequiredString(result, JsonFieldNames.LoginId, AppServerMethods.AccountLoginStart),
                AuthUrl = GetRequiredString(result, "authUrl", AppServerMethods.AccountLoginStart),
                Raw = result
            },
            "chatgptDeviceCode" => new AccountLoginStartResult.ChatGptDeviceCode
            {
                LoginId = GetRequiredString(result, JsonFieldNames.LoginId, AppServerMethods.AccountLoginStart),
                VerificationUrl = GetRequiredString(result, "verificationUrl", AppServerMethods.AccountLoginStart),
                UserCode = GetRequiredString(result, "userCode", AppServerMethods.AccountLoginStart),
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
            Status = ParseCancelStatus(GetStringOrNull(result, JsonFieldNames.Status)),
            Raw = result
        };

    private static AccountLoginCancelStatus ParseCancelStatus(string? value) =>
        value switch
        {
            "canceled" => AccountLoginCancelStatus.Canceled,
            "notFound" => AccountLoginCancelStatus.NotFound,
            _ => throw new InvalidOperationException(
                $"account/login/cancel returned an unknown status '{value ?? "<missing>"}'.")
        };
}
