using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Theory]
    [InlineData(-32601, "not found", true)]
    [InlineData(-32600, "unknown variant skills/remote/list", true)]
    [InlineData(-32600, "UNHANDLED SERVER REQUEST skills/remote/list", true)]
    [InlineData(-32600, "unknown variant another/method", false)]
    [InlineData(-32600, "ordinary failure skills/remote/list", false)]
    [InlineData(-32600, " ", false)]
    [InlineData(-32000, "unknown variant skills/remote/list", false)]
    public async Task RemoteSkills_FallsBackOnlyForUnsupportedCurrentMethod(int code, string message, bool fallback)
    {
        var failure = new JsonRpcRemoteException(new JsonRpcError(code, message, null));
        var rpc = new RecordingRpc("{}")
        {
            Respond = method => method == "skills/remote/list" ? throw failure : JsonDocument.Parse("""{"data":[]}""").RootElement.Clone()
        };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        if (fallback)
        {
            (await client.ReadRemoteSkillsAsync()).Skills.Should().BeEmpty();
            rpc.Requests.Select(x => x.Method).Should().Equal("skills/remote/list", "skills/remote/read");
        }
        else
        {
            var action = () => client.ReadRemoteSkillsAsync();
            (await action.Should().ThrowAsync<JsonRpcRemoteException>()).Which.Should().BeSameAs(failure);
            rpc.Requests.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task RemoteSkillExport_FallbackPreservesArgumentsAndResult()
    {
        var rpc = new RecordingRpc("{}")
        {
            Respond = method => method == "skills/remote/export"
                ? throw new JsonRpcRemoteException(new JsonRpcError(-32601, "not found", null))
                : JsonDocument.Parse("""{"id":"skill-1","name":"Example","path":"/skills/example"}""").RootElement.Clone()
        };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.WriteRemoteSkillAsync("hazelnut-1", true);
        result.Id.Should().Be("skill-1");
        result.Name.Should().Be("Example");
        result.Path.Should().Be(PathForPlatform("/skills/example"));
        rpc.Requests.Select(x => x.Method).Should().Equal("skills/remote/export", "skills/remote/write");
        JsonElement.DeepEquals(rpc.Requests[0].Parameters, rpc.Requests[1].Parameters).Should().BeTrue();
        rpc.Requests[1].Parameters.GetProperty("hazelnutId").GetString().Should().Be("hazelnut-1");
        rpc.Requests[1].Parameters.GetProperty("isPreload").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("notLoaded", ThreadUnsubscribeStatus.NotLoaded)]
    [InlineData("notSubscribed", ThreadUnsubscribeStatus.NotSubscribed)]
    [InlineData("unsubscribed", ThreadUnsubscribeStatus.Unsubscribed)]
    [InlineData("future", ThreadUnsubscribeStatus.Unknown)]
    [InlineData(null, ThreadUnsubscribeStatus.Unknown)]
    public async Task Unsubscribe_MapsEveryStatus(string? status, ThreadUnsubscribeStatus expected)
    {
        var rpc = new RecordingRpc(JsonSerializer.Serialize(new { status }));
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        (await client.UnsubscribeThreadAsync("thread-1")).Status.Should().Be(expected);
    }

    [Theory]
    [InlineData("reset", AccountRateLimitResetCreditConsumeOutcome.Reset)]
    [InlineData("nothingToReset", AccountRateLimitResetCreditConsumeOutcome.NothingToReset)]
    [InlineData("noCredit", AccountRateLimitResetCreditConsumeOutcome.NoCredit)]
    [InlineData("alreadyRedeemed", AccountRateLimitResetCreditConsumeOutcome.AlreadyRedeemed)]
    [InlineData("future", AccountRateLimitResetCreditConsumeOutcome.Unknown)]
    public async Task ResetCredit_MapsEveryOutcome(string outcome, AccountRateLimitResetCreditConsumeOutcome expected)
    {
        var rpc = new RecordingRpc(JsonSerializer.Serialize(new { outcome }));
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ConsumeAccountRateLimitResetCreditAsync("once-1");
        result.OutcomeKind.Should().Be(expected);
        result.Outcome.Should().Be(outcome);
    }

    [Theory]
    [InlineData("{}", "missing required property 'items'")]
    [InlineData("{\"items\":[42]}", "items[0] must be an object")]
    [InlineData("{\"items\":[{}]}", "missing required string property 'description'")]
    [InlineData("{\"items\":[{\"description\":\"d\",\"itemType\":\"future\"}]}", "unknown required itemType")]
    [InlineData("{\"items\":[{\"description\":\"d\",\"itemType\":\"CONFIG\",\"cwd\":42}]}", "must be a string or null")]
    public async Task ExternalConfig_RejectsMalformedResponses(string response, string error)
    {
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.DetectExternalAgentConfigAsync(new());
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{error}*");
        rpc.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ExternalConfig_DetectAndImport_PreservesOptionalWorkingDirectory()
    {
        var rpc = new RecordingRpc("""{"items":[{"description":"config","itemType":"CONFIG","cwd":"/workspace"},{"description":"skills","itemType":"SKILLS","cwd":null},{"description":"instructions","itemType":"AGENTS_MD"}]}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.DetectExternalAgentConfigAsync(new());
        result.Items.Select(x => x.Cwd).Should().Equal(PathForPlatform("/workspace"), null, null);
        result.Items.Select(x => x.Description).Should().Equal("config", "skills", "instructions");
        await client.ImportExternalAgentConfigAsync(result.Items);
        rpc.Requests[1].Method.Should().Be("externalAgentConfig/import");
        rpc.Requests[1].Parameters.GetProperty("migrationItems")[0].GetProperty("cwd").GetString().Should().Be(PathForPlatform("/workspace"));
    }

    public static IEnumerable<object[]> OverloadFailures()
    {
        foreach (var wrapped in new[] { false, true })
        {
            foreach (var message in new[] { "SERVER OVERLOADED", "overload", "backpressure", "retry later", "retry limit", "too many failed attempts" })
                yield return [wrapped, message, "null", true];
            foreach (var data in new[] { "{\"server_overloaded\":true}", "[42,{\"nested\":[\"response-too-many-failed-attempts\"]}]", "{\"message\":\"back pressure\"}" })
                yield return [wrapped, "failure", data, true];
            foreach (var data in new[] { "null", "{}", "[]", "[true,42,null,\"ordinary\",\" \"]", "{\"ordinary\":{\"value\":false}}" })
                yield return [wrapped, " ", data, false];
        }
    }

    [Theory]
    [MemberData(nameof(OverloadFailures))]
    public async Task RetryPolicy_RecognizesOnlyOverloadSignals(bool wrapped, string message, string data, bool retryable)
    {
        var rpc = new RecordingRpc("{}");
        var rawFailure = new JsonRpcRemoteException(new JsonRpcError(-32042, message, JsonDocument.Parse(data).RootElement.Clone()));
        rpc.Failure = rawFailure;
        var contexts = new List<CodexAppServerRetryContext>();
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc), new()
        {
            RetryPolicy = context => { contexts.Add(context); return ValueTask.FromResult(CodexAppServerRetryDecision.NoRetry); }
        });
        Func<Task> action = wrapped
            ? () => client.SteerTurnAsync(new() { ThreadId = "thread-1", ExpectedTurnId = "turn-1", Input = [TurnInputItem.Text("continue")] })
            : () => client.CallAsync("custom/read", null);
        if (wrapped)
            (await action.Should().ThrowAsync<CodexAppServerRequestFailedException>()).Which.InnerException.Should().BeSameAs(rawFailure);
        else
            (await action.Should().ThrowAsync<JsonRpcRemoteException>()).Which.Should().BeSameAs(rawFailure);
        contexts.Should().HaveCount(retryable ? 1 : 0);
        if (retryable)
        {
            contexts[0].OperationKind.Should().Be(wrapped ? CodexAppServerOperationKind.TurnControl : CodexAppServerOperationKind.Call);
            contexts[0].Attempt.Should().Be(1);
            await contexts[0].EnsureRestartedAsync(CancellationToken.None);
        }
        rpc.Requests.Should().ContainSingle();
        client.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task RetryPolicy_DelayAndHook_RunBeforeSecondRequest()
    {
        var rpc = new RecordingRpc("""{"result":123}""") { Failure = new JsonRpcRemoteException(new JsonRpcError(-32001, "server overloaded", null)) };
        var hookCalls = 0;
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc), new()
        {
            RetryPolicy = context =>
            {
                context.Attempt.Should().Be(1);
                return ValueTask.FromResult(CodexAppServerRetryDecision.Retry(TimeSpan.FromMilliseconds(1), token =>
                {
                    token.IsCancellationRequested.Should().BeFalse();
                    hookCalls++;
                    rpc.Requests.Should().ContainSingle();
                    rpc.Failure = null;
                    return Task.CompletedTask;
                }));
            }
        });
        var result = await client.CallAsync("custom/read", null).WaitAsync(TimeSpan.FromSeconds(5));
        result.GetProperty("result").GetInt32().Should().Be(123);
        hookCalls.Should().Be(1);
        rpc.Requests.Should().HaveCount(2);
    }
}
