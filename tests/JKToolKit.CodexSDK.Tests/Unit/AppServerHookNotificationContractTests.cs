using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerHookNotificationContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    private static JsonObject Run() => JsonNode.Parse("""{"id":"run","eventName":"SessionStart","handlerType":"command","executionMode":"sync","scope":"project","sourcePath":"hooks.json","displayOrder":4294967296,"status":"completed","startedAt":123,"entries":[{"kind":"stdout","text":"output"}]}""")!.AsObject();

    [Theory]
    [InlineData("hook/started")]
    [InlineData("hook/completed")]
    public void HookNotifications_MapClonedSummaryAndEntries(string method)
    {
        var run = Run(); run["statusMessage"] = "done"; run["completedAt"] = 234; run["durationMs"] = 111;
        var payload = new JsonObject { ["threadId"] = "thread", ["turnId"] = "turn", ["run"] = run };
        HookRunSummaryInfo info;
        JsonElement raw;
        using (var document = JsonDocument.Parse(payload.ToJsonString()))
        {
            var notification = AppServerNotificationMapper.Map(method, document.RootElement);
            if (method == "hook/started")
            {
                var hook = notification.Should().BeOfType<HookStartedNotification>().Subject;
                hook.ThreadId.Should().Be("thread"); hook.TurnId.Should().Be("turn"); info = hook.RunInfo; raw = hook.Run;
            }
            else
            {
                var hook = notification.Should().BeOfType<HookCompletedNotification>().Subject;
                hook.ThreadId.Should().Be("thread"); hook.TurnId.Should().Be("turn"); info = hook.RunInfo; raw = hook.Run;
            }
            notification.Method.Should().Be(method);
        }
        info.Id.Should().Be("run"); info.EventName.Should().Be("SessionStart"); info.HandlerType.Should().Be("command");
        info.ExecutionMode.Should().Be("sync"); info.Scope.Should().Be("project"); info.SourcePath.Should().Be("hooks.json");
        info.DisplayOrder.Should().Be(4294967296); info.Status.Should().Be("completed"); info.StartedAt.Should().Be(123);
        info.StatusMessage.Should().Be("done"); info.CompletedAt.Should().Be(234); info.DurationMs.Should().Be(111);
        var entry = info.Entries.Should().ContainSingle().Subject;
        entry.Kind.Should().Be("stdout"); entry.Text.Should().Be("output"); entry.Raw.GetProperty("text").GetString().Should().Be("output");
        info.Raw.GetProperty("id").GetString().Should().Be("run"); raw.GetProperty("status").GetString().Should().Be("completed");
    }

    [Theory]
    [InlineData("id")]
    [InlineData("eventName")]
    [InlineData("handlerType")]
    [InlineData("executionMode")]
    [InlineData("scope")]
    [InlineData("sourcePath")]
    [InlineData("displayOrder")]
    [InlineData("status")]
    [InlineData("startedAt")]
    [InlineData("entries")]
    public void HookSummary_RequiresEachFieldAndRejectsWrongTypes(string field)
    {
        foreach (var mode in new[] { "missing", "null", "wrong" })
        {
            var run = Run();
            if (mode == "missing") run.Remove(field);
            else run[field] = mode == "null" ? null : JsonValue.Create(true);
            var payload = Json(new JsonObject { ["threadId"] = "thread", ["run"] = run }.ToJsonString());
            AppServerNotificationParsing.TryParseHookRunSummaryInfo(payload, "run", out _, out _).Should().BeFalse($"{field} is {mode}");
            AppServerNotificationMapper.Map("hook/started", payload).Should().BeOfType<UnknownNotification>();
            AppServerNotificationMapper.Map("hook/completed", payload).Should().BeOfType<UnknownNotification>();
        }
    }

    [Theory]
    [InlineData("statusMessage", "false")]
    [InlineData("completedAt", "\"123\"")]
    [InlineData("completedAt", "1.5")]
    [InlineData("durationMs", "9223372036854775808")]
    [InlineData("entries", "[null]")]
    [InlineData("entries", "[{}]")]
    [InlineData("entries", "[{\"kind\":\"stdout\"}]")]
    [InlineData("entries", "[{\"kind\":\" \",\"text\":\"line\"}]")]
    [InlineData("entries", "[{\"kind\":\"stdout\",\"text\":\" \"}]")]
    public void HookSummary_RejectsMalformedOptionalValuesAndEntries(string field, string value)
    {
        var run = Run(); run[field] = JsonNode.Parse(value);
        AppServerNotificationParsing.TryParseHookRunSummaryInfo(Json(new JsonObject { ["run"] = run }.ToJsonString()), "run", out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run\":null}")]
    [InlineData("{\"run\":[]}")]
    public void HookSummary_MissingOrWrongKindRunFails(string payload) =>
        AppServerNotificationParsing.TryParseHookRunSummaryInfo(Json(payload), "run", out _, out _).Should().BeFalse();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HookSummary_AbsentOrNullOptionalFieldsRemainNull(bool explicitNull)
    {
        var run = Run(); run["entries"] = new JsonArray();
        if (explicitNull) { run["statusMessage"] = null; run["completedAt"] = null; run["durationMs"] = null; }
        var payload = Json(new JsonObject { ["threadId"] = "thread", ["turnId"] = null, ["run"] = run }.ToJsonString());
        var hook = AppServerNotificationMapper.Map("hook/completed", payload).Should().BeOfType<HookCompletedNotification>().Subject;
        hook.TurnId.Should().BeNull(); hook.RunInfo.StatusMessage.Should().BeNull(); hook.RunInfo.CompletedAt.Should().BeNull();
        hook.RunInfo.DurationMs.Should().BeNull(); hook.RunInfo.Entries.Should().BeEmpty();
    }
}
