using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Fact]
    public async Task TurnHandle_ControlOperations_UseOriginalThreadAndExpectedTurn()
    {
        var rpc = new RecordingRpc("{}")
        {
            Respond = method => JsonDocument.Parse(method switch
            {
                "turn/start" => """{"turn":{"id":"turn-1"}}""",
                "turn/steer" => """{"turnId":"turn-1"}""",
                _ => "{}"
            }).RootElement.Clone()
        };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        await using var turn = await client.StartTurnAsync("thread-1", new() { Input = [TurnInputItem.Text("begin")] });
        (await turn.SteerAsync([TurnInputItem.Text("continue")])).Should().Be("turn-1");
        (await turn.SteerRawAsync([TurnInputItem.Text("refine")])).TurnId.Should().Be("turn-1");
        (await client.SteerTurnRawAsync(new() { ThreadId = "thread-1", ExpectedTurnId = "turn-1", Input = [TurnInputItem.Text("finish")] })).TurnId.Should().Be("turn-1");
        await turn.InterruptAsync();
        rpc.Requests.Select(x => x.Method).Should().Equal("turn/start", "turn/steer", "turn/steer", "turn/steer", "turn/interrupt");
        foreach (var request in rpc.Requests)
            request.Parameters.GetProperty("threadId").GetString().Should().Be("thread-1");
        foreach (var request in rpc.Requests.Skip(1).Take(3))
            request.Parameters.GetProperty("expectedTurnId").GetString().Should().Be("turn-1");
        rpc.Requests[^1].Parameters.GetProperty("turnId").GetString().Should().Be("turn-1");
        await rpc.EmitAsync("turn/completed", JsonDocument.Parse("""{"threadId":"thread-1","turn":{"id":"turn-1","status":"interrupted"}}""").RootElement);
        (await turn.Completion.WaitAsync(TimeSpan.FromSeconds(5))).Status.Should().Be("interrupted");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Review_UsesServerChosenThread_AndCompletesFromNotifications(bool alias)
    {
        var rpc = new RecordingRpc("""{"reviewThreadId":"review-thread","turn":{"id":"review-turn"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var options = new ReviewStartOptions { ThreadId = "original-thread", Target = new ReviewTarget.UncommittedChanges() };
        var result = alias ? await client.ReviewAsync(options) : await client.StartReviewAsync(options);
        await using var turn = result.Turn;
        result.ReviewThreadId.Should().Be("review-thread");
        turn.ThreadId.Should().Be("review-thread");
        turn.TurnId.Should().Be("review-turn");
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("review/start");
        rpc.Requests[0].Parameters.GetProperty("threadId").GetString().Should().Be("original-thread");
        await rpc.EmitAsync("turn/completed", JsonDocument.Parse("""{"threadId":"review-thread","turn":{"id":"review-turn","status":"completed"}}""").RootElement);
        (await turn.Completion.WaitAsync(TimeSpan.FromSeconds(5))).Status.Should().Be("completed");
    }

    [Theory]
    [InlineData("{}", "review thread id")]
    [InlineData("{\"reviewThreadId\":\"review-thread\"}", "turn id")]
    public async Task Review_RejectsMalformedStartupResponse(string response, string missing)
    {
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.StartReviewAsync(new() { ThreadId = "thread-1", Target = new ReviewTarget.UncommittedChanges() });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*returned no {missing}*");
    }

    [Fact]
    public async Task Review_RpcFailure_PreservesMethodAndRemoteError()
    {
        var failure = new JsonRpcRemoteException(new JsonRpcError(-32602, "invalid target", JsonSerializer.SerializeToElement(new { field = "target" })));
        var rpc = new RecordingRpc("{}") { Failure = failure };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.StartReviewAsync(new() { ThreadId = "thread-1", Target = new ReviewTarget.UncommittedChanges() });
        var error = (await action.Should().ThrowAsync<CodexAppServerRequestFailedException>()).Which;
        error.Method.Should().Be("review/start");
        error.ErrorCode.Should().Be(-32602);
        error.ErrorMessage.Should().Be("invalid target");
        error.ErrorData!.Value.GetProperty("field").GetString().Should().Be("target");
        error.InnerException.Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData(" ", "\"output\"", false)]
    [InlineData("tool", "null", false)]
    [InlineData("tool", "{}", false)]
    [InlineData("tool", "\"output\"", true)]
    public async Task TurnToolOutput_RejectsInvalidShapeAndMixedInput(string name, string output, bool mixed)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.StartTurnAsync("thread-1", new()
        {
            ToolOutput = new() { Name = name, Output = JsonDocument.Parse(output).RootElement.Clone() },
            Input = mixed ? [TurnInputItem.Text("mixed")] : []
        });
        await action.Should().ThrowAsync<ArgumentException>();
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneShotFuzzySearch_PreservesOptionalCancellationToken(bool useCancellationToken)
    {
        var rpc = new RecordingRpc("""{"files":[]}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        (await client.FuzzyFileSearchAsync("readme", ["/workspace"], useCancellationToken ? "search-1" : " ")).Should().BeEmpty();
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("fuzzyFileSearch");
        rpc.Requests[0].Parameters.GetProperty("query").GetString().Should().Be("readme");
        rpc.Requests[0].Parameters.GetProperty("roots")[0].GetString().Should().Be("/workspace");
        rpc.Requests[0].Parameters.TryGetProperty("cancellationToken", out var token).Should().Be(useCancellationToken);
        if (useCancellationToken) token.GetString().Should().Be("search-1");
    }
}
