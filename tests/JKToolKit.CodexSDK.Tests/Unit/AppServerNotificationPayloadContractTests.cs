using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationPayloadContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("42")]
    [InlineData("[]")]
    [InlineData("\"future\"")]
    public void ThreadSettings_MalformedNestedPayloadDoesNotCrashDispatch(string settings)
    {
        var notification = AppServerNotificationMapper.Map("thread/settings/updated", Json("{\"threadId\":\"t\",\"threadSettings\":" + settings + "}"))
            .Should().BeOfType<ThreadSettingsUpdatedNotification>().Subject;
        notification.ThreadId.Should().Be("t"); notification.ThreadSettings.GetRawText().Should().Be(settings);
        notification.Cwd.Should().BeNull(); notification.Model.Should().BeNull(); notification.ServiceTier.Should().BeNull();
        notification.DisabledPluginIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("null", null, null)]
    [InlineData("{}", null, null)]
    [InlineData("{\"id\":42,\"type\":false}", null, null)]
    [InlineData("{\"id\":\"item\",\"type\":\"agentMessage\"}", "item", "agentMessage")]
    public void ItemNotifications_ExposeIdentifiersOnlyForStringFields(string item, string? id, string? type)
    {
        var payload = Json("{\"threadId\":\"thread\",\"turnId\":\"turn\",\"item\":" + item + "}");
        var started = AppServerNotificationMapper.Map("item/started", payload).Should().BeOfType<ItemStartedNotification>().Subject;
        var completed = AppServerNotificationMapper.Map("item/completed", payload).Should().BeOfType<ItemCompletedNotification>().Subject;
        started.ThreadId.Should().Be("thread"); started.TurnId.Should().Be("turn"); started.ItemId.Should().Be(id); started.ItemType.Should().Be(type);
        completed.ThreadId.Should().Be("thread"); completed.TurnId.Should().Be("turn"); completed.ItemId.Should().Be(id); completed.ItemType.Should().Be(type);
        started.Item.GetRawText().Should().Be(item); completed.Item.GetRawText().Should().Be(item);
    }

    [Fact]
    public void PlanNotification_MapsOrderedStepsAndFiltersNonObjects()
    {
        var plan = AppServerNotificationMapper.Map("turn/plan/updated", Json("""{"threadId":"t","turnId":"u","explanation":"why","plan":[null,42,{}, {"step":"test","status":"inProgress"}]}"""))
            .Should().BeOfType<TurnPlanUpdatedNotification>().Subject;
        plan.ThreadId.Should().Be("t"); plan.TurnId.Should().Be("u"); plan.Explanation.Should().Be("why");
        plan.Plan.Should().HaveCount(2); plan.Plan[0].Step.Should().BeEmpty(); plan.Plan[0].Status.Should().BeEmpty();
        plan.Plan[1].Step.Should().Be("test"); plan.Plan[1].Status.Should().Be("inProgress");
        Action missing = () => new TurnPlanUpdatedNotification("t", "u", null, null!, Json("{}"));
        missing.Should().Throw<ArgumentNullException>().WithParameterName("Plan");
    }

    [Theory]
    [InlineData("stdout", CommandExecOutputStreamKind.Stdout)]
    [InlineData(" stderr ", CommandExecOutputStreamKind.Stderr)]
    [InlineData("future", CommandExecOutputStreamKind.Unknown)]
    public void CommandExec_MapsOutputStreamAndCap(string stream, CommandExecOutputStreamKind kind)
    {
        var notification = AppServerNotificationMapper.Map("command/exec/outputDelta", JsonSerializer.SerializeToElement(new { processId = "p", stream, deltaBase64 = "AA==", capReached = true }))
            .Should().BeOfType<CommandExecOutputDeltaNotification>().Subject;
        notification.ProcessId.Should().Be("p"); notification.Stream.Should().Be(stream); notification.StreamKind.Should().Be(kind);
        notification.DeltaBase64.Should().Be("AA=="); notification.CapReached.Should().BeTrue();
    }

    [Fact]
    public void CommandNotifications_KeepTerminalAndOutputFieldsDistinct()
    {
        var output = AppServerNotificationMapper.Map("item/commandExecution/outputDelta", Json("""{"threadId":"t","turnId":"u","itemId":"i","delta":"stdout"}"""))
            .Should().BeOfType<CommandExecutionOutputDeltaNotification>().Subject;
        output.ThreadId.Should().Be("t"); output.TurnId.Should().Be("u"); output.ItemId.Should().Be("i"); output.Delta.Should().Be("stdout");
        var terminal = AppServerNotificationMapper.Map("item/commandExecution/terminalInteraction", Json("""{"threadId":"t","turnId":"u","itemId":"i","processId":"p","stdin":"input"}"""))
            .Should().BeOfType<TerminalInteractionNotification>().Subject;
        terminal.ThreadId.Should().Be("t"); terminal.TurnId.Should().Be("u"); terminal.ItemId.Should().Be("i"); terminal.ProcessId.Should().Be("p"); terminal.Stdin.Should().Be("input");
        var file = AppServerNotificationMapper.Map("item/fileChange/outputDelta", Json("""{"threadId":"t","turnId":"u","itemId":"i","delta":"patch"}"""))
            .Should().BeOfType<FileChangeOutputDeltaNotification>().Subject;
        file.ThreadId.Should().Be("t"); file.TurnId.Should().Be("u"); file.ItemId.Should().Be("i"); file.Delta.Should().Be("patch");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", false)]
    public void SetupAndErrorNotifications_UseBooleanWireValues(string boolean, bool expected)
    {
        var setup = AppServerNotificationMapper.Map("windowsSandbox/setupCompleted", Json("{\"mode\":\"elevated\",\"success\":" + boolean + ",\"error\":\"setup error\"}"))
            .Should().BeOfType<WindowsSandboxSetupCompletedNotification>().Subject;
        setup.Mode.Should().Be("elevated"); setup.Success.Should().Be(expected); setup.Error.Should().Be("setup error");
        var error = AppServerNotificationMapper.Map("error", Json("{\"threadId\":\"t\",\"turnId\":\"u\",\"willRetry\":" + boolean + ",\"error\":{\"message\":\"failed\"}}"))
            .Should().BeOfType<ErrorNotification>().Subject;
        error.ThreadId.Should().Be("t"); error.TurnId.Should().Be("u"); error.WillRetry.Should().Be(expected); error.Error.GetProperty("message").GetString().Should().Be("failed");
    }

    [Theory]
    [InlineData("123", 123L)]
    [InlineData("\"456\"", 456L)]
    [InlineData("\"bad\"", 0L)]
    [InlineData("1.5", 0L)]
    [InlineData("null", 0L)]
    public void ReasoningIndex_ParsesNumericAndLegacyStringValues(string value, long expected)
    {
        var notification = AppServerNotificationMapper.Map("item/reasoning/textDelta", Json("{\"threadId\":\"t\",\"turnId\":\"u\",\"itemId\":\"i\",\"delta\":\"reason\",\"contentIndex\":" + value + "}"))
            .Should().BeOfType<ReasoningTextDeltaNotification>().Subject;
        notification.ThreadId.Should().Be("t"); notification.TurnId.Should().Be("u"); notification.ItemId.Should().Be("i");
        notification.Delta.Should().Be("reason"); notification.ContentIndex.Should().Be(expected);
    }

    [Fact]
    public void ConfigWarning_ClonesOptionalRange()
    {
        var warning = AppServerNotificationMapper.Map("configWarning", Json("""{"summary":"Summary","details":"Details","path":"config.toml","range":{"start":2}}"""))
            .Should().BeOfType<ConfigWarningNotification>().Subject;
        warning.Summary.Should().Be("Summary"); warning.Details.Should().Be("Details"); warning.Path.Should().Be("config.toml"); warning.Range!.Value.GetProperty("start").GetInt32().Should().Be(2);
    }

    [Fact]
    public void RestartMarker_SerializesUtcTimestampAndTail()
    {
        var timestamp = new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.FromHours(2));
        var notification = new ClientRestartedNotification(3, 7, timestamp, "exited", ["last line"]);
        notification.RestartCount.Should().Be(3); notification.PreviousExitCode.Should().Be(7); notification.Timestamp.Should().Be(timestamp);
        notification.Reason.Should().Be("exited"); notification.PreviousStderrTail.Should().Equal("last line"); notification.Method.Should().Be("client/restarted");
        notification.Params.GetProperty("timestamp").GetDateTime().Should().Be(timestamp.UtcDateTime);
        notification.Params.GetProperty("previousStderrTail")[0].GetString().Should().Be("last line");
        var empty = new ClientRestartedNotification(1, null, timestamp, null, null);
        empty.PreviousStderrTail.Should().BeEmpty(); empty.Params.GetProperty("previousStderrTail").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("thread/realtime/started", "{\"threadId\":\"t\",\"version\":\"v\",\"sessionId\":false}")]
    [InlineData("thread/realtime/outputAudio/delta", "{\"threadId\":\"t\",\"audio\":{\"data\":\"AA==\",\"numChannels\":1,\"sampleRate\":24000,\"samplesPerChannel\":-1}}")]
    [InlineData("thread/realtime/outputAudio/delta", "{\"threadId\":\"t\",\"audio\":{\"data\":\"AA==\",\"numChannels\":-1,\"sampleRate\":24000}}")]
    [InlineData("fs/changed", "{\"watchId\":\"w\",\"changedPaths\":[false]}")]
    [InlineData("mcpServer/startupStatus/updated", "{\"name\":\"server\",\"status\":\"future\"}")]
    public void StrictPayloads_RejectMalformedNestedValues(string method, string json) =>
        AppServerNotificationMapper.Map(method, Json(json)).Should().BeOfType<UnknownNotification>();

    [Fact]
    public void AudioDelta_ZeroSamplesAndExplicitNullCountsAreValid()
    {
        foreach (var count in new[] { "0", "null" })
        {
            var payload = Json("{\"threadId\":\"t\",\"audio\":{\"data\":\"\",\"numChannels\":0,\"sampleRate\":0,\"samplesPerChannel\":" + count + "}}");
            var audio = AppServerNotificationMapper.Map("thread/realtime/outputAudio/delta", payload).Should().BeOfType<ThreadRealtimeOutputAudioDeltaNotification>().Subject;
            audio.SampleRate.Should().Be(0); audio.NumChannels.Should().Be(0); audio.SamplesPerChannel.Should().Be(count == "0" ? 0 : null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModelSafetyBuffering_PreservesExplicitBooleanValues(bool enabled)
    {
        var payload = JsonSerializer.SerializeToElement(new { threadId = "t", turnId = "u", model = "m", showBufferingUi = enabled, useCases = new[] { "case" }, reasons = new[] { "reason" }, fasterModel = "fast" });
        var notification = AppServerNotificationMapper.Map("model/safetyBuffering/updated", payload).Should().BeOfType<ModelSafetyBufferingUpdatedNotification>().Subject;
        notification.ThreadId.Should().Be("t"); notification.TurnId.Should().Be("u"); notification.Model.Should().Be("m"); notification.ShowBufferingUi.Should().Be(enabled);
        notification.UseCases.Should().Equal("case"); notification.Reasons.Should().Equal("reason"); notification.FasterModel.Should().Be("fast");
    }

    [Fact]
    public void HistoryTurn_ParsesStatusItemsAndErrorAsOneSnapshot()
    {
        CodexTurn turn;
        using (var document = JsonDocument.Parse("""{"id":"turn","status":"failed","items":[{"id":"i","type":"plan","text":"plan"}],"error":{"message":"failure"}}"""))
            turn = CodexTurn.TryParse(document.RootElement)!;
        turn.Id.Should().Be("turn"); turn.Status.Should().Be(CodexTurnStatus.Failed); turn.Error!.Message.Should().Be("failure");
        turn.Items.Should().ContainSingle().Which.Id.Should().Be("i"); turn.Raw.GetProperty("status").GetString().Should().Be("failed");
        CodexTurn.TryParse(Json("null")).Should().BeNull();
    }
}
