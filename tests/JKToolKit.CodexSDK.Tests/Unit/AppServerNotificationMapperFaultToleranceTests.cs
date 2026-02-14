using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationMapperFaultToleranceTests
{
    private static readonly string[] KnownMethods =
    [
        "error",
        "thread/started",
        "thread/name/updated",
        "thread/tokenUsage/updated",
        "turn/started",
        "turn/completed",
        "turn/diff/updated",
        "turn/plan/updated",
        "item/started",
        "item/completed",
        "rawResponseItem/completed",
        "item/agentMessage/delta",
        "item/plan/delta",
        "item/commandExecution/outputDelta",
        "item/commandExecution/terminalInteraction",
        "item/fileChange/outputDelta",
        "item/mcpToolCall/progress",
        "mcpServer/oauthLogin/completed",
        "account/updated",
        "account/rateLimits/updated",
        "item/reasoning/summaryTextDelta",
        "item/reasoning/summaryPartAdded",
        "item/reasoning/textDelta",
        "thread/compacted",
        "deprecationNotice",
        "configWarning",
        "windows/worldWritableWarning",
        "account/login/completed",
        "authStatusChange",
        "loginChatGptComplete",
        "sessionConfigured",
        "app/list/updated"
    ];

    public static IEnumerable<object[]> BogusParams()
    {
        yield return [Parse("null")];
        yield return [Parse("123")];
        yield return [Parse("\"x\"")];
        yield return [Parse("true")];
        yield return [Parse("[]")];
        yield return [Parse("{}")];
        yield return [Parse("""{"threadId":123,"turnId":false,"itemId":{},"delta":[]}""")];
        yield return [Parse("""{"plan":{}}""")];
        yield return [Parse("""{"samplePaths":123,"extraCount":"x","failedScan":{}}""")];
        yield return [Parse("""{"turn":123,"threadId":{}}""")];
        yield return [Parse("""{"item":123,"threadId":{}}""")];
    }

    [Theory]
    [MemberData(nameof(BogusParams))]
    public void Map_KnownMethods_NeverThrows_OnBogusParams(JsonElement bogus)
    {
        foreach (var method in KnownMethods)
        {
            var act = () => AppServerNotificationMapper.Map(method, bogus);
            act.Should().NotThrow($"method '{method}' should be fault-tolerant");
        }
    }

    [Fact]
    public void Map_NullParams_NeverThrows()
    {
        foreach (var method in KnownMethods)
        {
            var act = () => AppServerNotificationMapper.Map(method, @params: null);
            act.Should().NotThrow();
            AppServerNotificationMapper.Map(method, @params: null).Should().BeOfType<UnknownNotification>();
        }
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
