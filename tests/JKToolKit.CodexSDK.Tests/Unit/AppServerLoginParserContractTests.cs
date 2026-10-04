using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerLoginParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    private sealed record FutureLogin : AccountLoginStartOptions;

    [Fact]
    public void LoginOptions_EmitDistinctWireContracts()
    {
        JsonSerializer.SerializeToElement(CodexAppServerAccountLoginParsers.BuildStartParams(new AccountLoginStartOptions.ApiKey("key")))
            .GetRawText().Should().Be("{\"type\":\"apiKey\",\"apiKey\":\"key\"}");
        JsonSerializer.SerializeToElement(CodexAppServerAccountLoginParsers.BuildStartParams(new AccountLoginStartOptions.ChatGptBrowser()))
            .GetRawText().Should().Be("{\"type\":\"chatgpt\"}");
        JsonSerializer.SerializeToElement(CodexAppServerAccountLoginParsers.BuildStartParams(new AccountLoginStartOptions.ChatGptDeviceCode()))
            .GetRawText().Should().Be("{\"type\":\"chatgptDeviceCode\"}");
        JsonSerializer.SerializeToElement(CodexAppServerAccountLoginParsers.BuildStartParams(new AccountLoginStartOptions.ChatGptAuthTokens("token", "account", "plus")))
            .GetRawText().Should().Be("{\"type\":\"chatgptAuthTokens\",\"accessToken\":\"token\",\"chatgptAccountId\":\"account\",\"chatgptPlanType\":\"plus\"}");
        Action future = () => CodexAppServerAccountLoginParsers.BuildStartParams(new FutureLogin());
        future.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("options");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void LoginOptions_RejectMissingCredentials(string? value)
    {
        Action apiKey = () => new AccountLoginStartOptions.ApiKey(value!);
        Action token = () => new AccountLoginStartOptions.ChatGptAuthTokens(value!, "account");
        Action account = () => new AccountLoginStartOptions.ChatGptAuthTokens("token", value!);
        apiKey.Should().Throw<ArgumentException>().WithParameterName("apiKey");
        token.Should().Throw<ArgumentException>().WithParameterName("accessToken");
        account.Should().Throw<ArgumentException>().WithParameterName("chatGptAccountId");
    }

    [Fact]
    public void LoginResults_ExposeBrowserDeviceCodeAndImmediateSuccess()
    {
        var browser = CodexAppServerAccountLoginParsers.ParseStartResult(Json("""{"type":"chatgpt","loginId":"b","authUrl":"https://auth.test"}"""))
            .Should().BeOfType<AccountLoginStartResult.ChatGptBrowser>().Subject;
        browser.LoginId.Should().Be("b"); browser.AuthUrl.Should().Be("https://auth.test");
        var device = CodexAppServerAccountLoginParsers.ParseStartResult(Json("""{"type":"chatgptDeviceCode","loginId":"d","verificationUrl":"https://device.test","userCode":"ABC"}"""))
            .Should().BeOfType<AccountLoginStartResult.ChatGptDeviceCode>().Subject;
        device.LoginId.Should().Be("d"); device.VerificationUrl.Should().Be("https://device.test"); device.UserCode.Should().Be("ABC");
        CodexAppServerAccountLoginParsers.ParseStartResult(Json("""{"type":"apiKey"}"""))
            .Should().BeOfType<AccountLoginStartResult.ApiKey>().Which.Raw.GetProperty("type").GetString().Should().Be("apiKey");
        CodexAppServerAccountLoginParsers.ParseStartResult(Json("""{"type":"chatgptAuthTokens"}"""))
            .Should().BeOfType<AccountLoginStartResult.ChatGptAuthTokens>().Which.Raw.GetProperty("type").GetString().Should().Be("chatgptAuthTokens");
    }

    [Theory]
    [InlineData("{}", "unknown login type '<missing>'")]
    [InlineData("{\"type\":\"future\"}", "unknown login type 'future'")]
    [InlineData("{\"type\":\"chatgpt\"}", "loginId")]
    [InlineData("{\"type\":\"chatgpt\",\"loginId\":\"b\"}", "authUrl")]
    [InlineData("{\"type\":\"chatgptDeviceCode\"}", "loginId")]
    [InlineData("{\"type\":\"chatgptDeviceCode\",\"loginId\":\"d\"}", "verificationUrl")]
    [InlineData("{\"type\":\"chatgptDeviceCode\",\"loginId\":\"d\",\"verificationUrl\":\"url\"}", "userCode")]
    public void LoginResults_RejectIncompleteAndUnknownVariants(string json, string error)
    {
        Action parse = () => CodexAppServerAccountLoginParsers.ParseStartResult(Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Theory]
    [InlineData("canceled", AccountLoginCancelStatus.Canceled)]
    [InlineData("notFound", AccountLoginCancelStatus.NotFound)]
    public void Cancel_MapsStatuses(string status, AccountLoginCancelStatus expected)
    {
        var result = CodexAppServerAccountLoginParsers.ParseCancelResult(JsonSerializer.SerializeToElement(new { status }));
        result.Status.Should().Be(expected); result.Raw.GetProperty("status").GetString().Should().Be(status);
    }

    [Theory]
    [InlineData("{}", "<missing>")]
    [InlineData("{\"status\":\"future\"}", "future")]
    public void Cancel_RejectsUnknownStatuses(string json, string value)
    {
        Action parse = () => CodexAppServerAccountLoginParsers.ParseCancelResult(Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*unknown status '" + value + "'*");
    }
}
