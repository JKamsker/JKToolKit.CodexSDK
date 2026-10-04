using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Fact]
    public async Task RetryDelay_IsCancelableBeforeHookOrRedispatch()
    {
        using var cts = new CancellationTokenSource();
        var decisionMade = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hookCalls = 0;
        var rpc = new RecordingRpc("{}") { Failure = new JsonRpcRemoteException(new JsonRpcError(-32001, "server overloaded", null)) };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc), new()
        {
            RetryPolicy = _ =>
            {
                decisionMade.TrySetResult();
                return ValueTask.FromResult(CodexAppServerRetryDecision.Retry(TimeSpan.FromDays(1), _ =>
                {
                    hookCalls++;
                    rpc.Failure = null;
                    return Task.CompletedTask;
                }));
            }
        });
        var request = client.CallAsync("custom/read", null, cts.Token);
        await decisionMade.Task.WaitAsync(TimeSpan.FromSeconds(5));
        request.IsCompleted.Should().BeFalse();
        cts.Cancel();
        var action = async () => await request.WaitAsync(TimeSpan.FromSeconds(5));
        await action.Should().ThrowAsync<OperationCanceledException>();
        hookCalls.Should().Be(0);
        rpc.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RequestPipeline_UsesCustomSerializationPolicyForObjects()
    {
        var observer = new Observer();
        var rpc = new RecordingRpc("{}");
        await using var client = new CodexAppServerClient(new()
        {
            SerializerOptionsOverride = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            RequestParamsTransformers = [new IdentityRequestTransformer()],
            MessageObservers = [observer]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await client.CallAsync("custom/read", new { ThreadId = "thread-1" });
        observer.Requests.Should().ContainSingle();
        observer.Requests[0].GetProperty("thread_id").GetString().Should().Be("thread-1");
        rpc.Requests[0].Parameters.GetProperty("thread_id").GetString().Should().Be("thread-1");
        rpc.Requests[0].Parameters.TryGetProperty("threadId", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("start")]
    [InlineData("update")]
    [InlineData("stop")]
    public async Task FuzzySession_InvalidIdentifier_ExplainsTheRejectedField(string operation)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> action = operation switch
        {
            "start" => () => client.StartFuzzyFileSearchSessionAsync(" ", [PathForPlatform("/workspace")]),
            "update" => () => client.UpdateFuzzyFileSearchSessionAsync(" ", "query"),
            _ => () => client.StopFuzzyFileSearchSessionAsync(" ")
        };
        var error = (await action.Should().ThrowAsync<ArgumentException>().WithMessage("*SessionId cannot be empty or whitespace*")).Which;
        error.ParamName.Should().Be("sessionId");
        rpc.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FuzzySession_NullRoots_ThrowsArgumentNullBeforeDispatch()
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var action = () => client.StartFuzzyFileSearchSessionAsync("search-1", null!);
        (await action.Should().ThrowAsync<ArgumentNullException>()).Which.ParamName.Should().Be("roots");
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestPipeline_AlreadyEncodedJsonReachesTransformersWithoutCustomObjectConversion(bool documentInput)
    {
        using var document = JsonDocument.Parse("""{"threadId":"thread-1"}""");
        var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializer.Converters.Add(new RejectReencodingConverter<JsonElement>());
        serializer.Converters.Add(new RejectReencodingConverter<JsonDocument>());
        var rpc = new RecordingRpc("{}");
        await using var client = new CodexAppServerClient(new()
        {
            SerializerOptionsOverride = serializer,
            RequestParamsTransformers = [new IdentityRequestTransformer()]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await client.CallAsync("custom/read", documentInput ? document : document.RootElement);
        rpc.Requests.Should().ContainSingle();
        rpc.Requests[0].Parameters.GetProperty("threadId").GetString().Should().Be("thread-1");
    }

    private sealed class RejectReencodingConverter<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => throw new InvalidOperationException("Raw JSON should reach the request transformer directly.");
    }

    private sealed class IdentityRequestTransformer : JKToolKit.CodexSDK.AppServer.Overrides.IAppServerRequestParamsTransformer
    {
        public JsonElement Transform(string method, JsonElement parameters) => parameters;
    }
}
