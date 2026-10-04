using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

#pragma warning disable MAAI001

public sealed class AgentFrameworkRunIntegrationTests
{
    [Fact]
    public async Task RunAsync_CreatesThreadWithEffectiveOptions_ThenResumesWithoutOverwritingSessionMetadata()
    {
        await using var fixture = new Fixture();
        var history = new History();
        var context = new Context();
        var tool = AIFunctionFactory.Create(() => "local value", "read_value");
        var options = new CodexAIAgentOptions
        {
            Id = "agent-id", Name = "Agent name", Model = "gpt-5.5", Cwd = "/default",
            ApprovalPolicy = CodexApprovalPolicy.Never, Sandbox = CodexSandboxMode.ReadOnly,
            Effort = CodexReasoningEffort.Low, Summary = "auto", Instructions = "base instructions",
            ChatHistoryProvider = history, AIContextProviders = [context], Tools = [tool],
            ConfigureThread = thread => thread.Cwd = "/thread-configured",
            ConfigureTurn = turn => turn.Summary = "concise"
        };
        var agent = fixture.Sdk.AsAIAgent(options);
        var session = (CodexAgentSession)await agent.CreateSessionAsync();
        var runOptions = new CodexAgentRunOptions
        {
            Model = "gpt-5.4", Cwd = "/run", Effort = CodexReasoningEffort.High,
            ConfigureTurn = turn => turn.Summary = "detailed"
        };

        var response = await agent.RunAsync([new ChatMessage(ChatRole.User, "hello")], session, runOptions);

        response.Text.Should().Be("hello world");
        response.AgentId.Should().Be("agent-id");
        response.ResponseId.Should().Be("turn-1");
        session.ThreadId.Should().Be("thread-1");
        session.Model.Should().Be("gpt-5.4");
        session.Cwd.Should().Be("/run");
        session.ApprovalPolicy.Should().Be(CodexApprovalPolicy.Never);
        session.Sandbox.Should().Be(CodexSandboxMode.ReadOnly);
        session.ToolSchemaHash.Should().NotBeNullOrWhiteSpace();
        session.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        var createdAt = session.CreatedAt;
        fixture.Rpcs[0].Requests.Select(x => x.Method).Should().ContainInOrder("thread/start", "turn/start");
        var thread = fixture.Rpcs[0].Requests.Single(x => x.Method == "thread/start").Parameters;
        thread.GetProperty("cwd").GetString().Should().Be("/thread-configured");
        thread.GetProperty("model").GetString().Should().Be("gpt-5.4");
        thread.GetProperty("dynamicTools").GetArrayLength().Should().Be(1);
        var turn = fixture.Rpcs[0].Requests.Single(x => x.Method == "turn/start").Parameters;
        turn.GetProperty("summary").GetString().Should().Be("detailed");
        turn.GetProperty("effort").GetString().Should().Be("high");
        turn.GetProperty("input").GetRawText().Should().Contain("remembered").And.Contain("hello").And.Contain("context message");
        history.Completed.Should().ContainSingle().Which.InvokeException.Should().BeNull();
        context.Completed.Should().ContainSingle().Which.ResponseMessages!.Single().Text.Should().Be("hello world");
        fixture.ClientOptions[0].ExperimentalApi.Should().BeTrue();
        fixture.ClientOptions[0].ApprovalHandler.Should().NotBeNull();
        fixture.Rpcs[0].Disposed.Should().BeTrue();

        var second = await agent.RunAsync("again", session, new CodexAgentRunOptions { Model = "gpt-5.3" });

        second.Text.Should().Be("hello world");
        fixture.Rpcs[1].Requests.Select(x => x.Method).Should().Contain("thread/resume").And.NotContain("thread/start");
        session.Model.Should().Be("gpt-5.4");
        session.CreatedAt.Should().Be(createdAt);
        history.Completed.Should().HaveCount(2);
    }

    [Fact]
    public async Task Streaming_WithoutExplicitSession_EmitsOrderedUpdatesAndDisposesClient()
    {
        await using var fixture = new Fixture();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions());
        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in agent.RunStreamingAsync(Messages())) updates.Add(update);

        updates.Select(x => x.Text).Should().Equal("hello ", "world");
        updates.Should().OnlyContain(x => x.AgentId == "codex" && x.AuthorName == "Codex" && x.ResponseId == "turn-1" && x.MessageId == "message-1");
        fixture.Rpcs.Single().Disposed.Should().BeTrue();
        fixture.ClientOptions.Single().ExperimentalApi.Should().BeFalse();
        agent.Description.Should().Be("Codex CLI agent.");
        agent.GetService(typeof(IDisposable), "unknown").Should().BeNull();

        static IEnumerable<ChatMessage> Messages() { yield return new(ChatRole.User, "hello"); }
    }

    [Fact]
    public async Task Streaming_ServerErrors_PreservesBothNotificationAndTerminalErrorContent()
    {
        await using var fixture = new Fixture { ReportErrors = true };
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions { Id = "errors", Name = "Reporter" });
        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in agent.RunStreamingAsync("hello")) updates.Add(update);
        var errors = updates.Where(x => x.Contents.OfType<ErrorContent>().Any()).ToArray();
        errors.Should().HaveCount(2);
        errors[0].Contents.OfType<ErrorContent>().Single().Message.Should().Contain("temporary failure");
        errors[1].Contents.OfType<ErrorContent>().Single().Message.Should().Contain("terminal failure");
        errors.Should().OnlyContain(x => x.AgentId == "errors" && x.AuthorName == "Reporter" && x.ResponseId == "turn-1");
        errors[1].FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Streaming_Cancellation_NotifiesContextProvidersAndDisposesClient(bool withProviders)
    {
        await using var fixture = new Fixture { AutoComplete = false };
        var history = new History();
        var context = new Context();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions
        {
            ChatHistoryProvider = withProviders ? history : null,
            AIContextProviders = withProviders ? [context] : null
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var updates = agent.RunStreamingAsync("hello", cancellationToken: cts.Token).GetAsyncEnumerator();
        (await updates.MoveNextAsync()).Should().BeTrue();
        cts.Cancel();
        var act = async () => await updates.MoveNextAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
        if (withProviders)
        {
            history.Completed.Should().ContainSingle().Which.InvokeException.Should().BeAssignableTo<OperationCanceledException>();
            context.Completed.Should().ContainSingle().Which.InvokeException.Should().BeAssignableTo<OperationCanceledException>();
        }
        fixture.Rpcs.Single().Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task ResumedSession_WithUnknownToolSchema_RejectsPerRunToolsBeforeStartingClient()
    {
        await using var fixture = new Fixture();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions());
        var act = () => agent.RunAsync("hello", new CodexAgentSession { ThreadId = "existing" },
            new CodexAgentRunOptions { Tools = [AIFunctionFactory.Create(() => "ok", "read")] });
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*thread is created*");
        fixture.Rpcs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyThreadId_IsTreatedAsNewSession_WhenPerRunToolsAreConfigured(string threadId)
    {
        await using var fixture = new Fixture();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions());
        var session = new CodexAgentSession { ThreadId = threadId };
        var response = await agent.RunAsync("hello", session,
            new CodexAgentRunOptions { Tools = [AIFunctionFactory.Create(() => "ok", "read")] });
        response.Text.Should().Be("hello world");
        session.ThreadId.Should().Be("thread-1");
        fixture.Rpcs.Single().Requests.Should().Contain(x => x.Method == "thread/start");
    }

    [Fact]
    public async Task ResumedSession_WithUnknownToolSchema_AllowsRunWithoutTools()
    {
        await using var fixture = new Fixture();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions());
        var response = await agent.RunAsync("hello", new CodexAgentSession { ThreadId = "existing" });
        response.Text.Should().Be("hello world");
        fixture.Rpcs.Single().Requests.Should().Contain(x => x.Method == "thread/resume");
    }

    [Theory]
    [InlineData("client")]
    [InlineData("thread/start")]
    [InlineData("turn/start")]
    public async Task RunAsync_StartupFailure_NotifiesPreparedContextProviders(string stage)
    {
        await using var fixture = new Fixture { FailureStage = stage };
        var history = new History();
        var context = new Context();
        var agent = fixture.Sdk.AsAIAgent(new CodexAIAgentOptions
        {
            ChatHistoryProvider = history, AIContextProviders = [context]
        });
        var run = () => agent.RunAsync("hello");
        var failure = (await run.Should().ThrowAsync<IOException>()).Which;
        history.Completed.Should().ContainSingle().Which.InvokeException.Should().BeSameAs(failure);
        context.Completed.Should().ContainSingle().Which.InvokeException.Should().BeSameAs(failure);
        fixture.Rpcs.Should().OnlyContain(rpc => rpc.Disposed);
    }

    [Fact]
    public async Task PublicSessionOperations_RejectOtherAgentSessionTypes()
    {
        var agent = new CodexAgentClient().AsAIAgent();
        var other = new OtherSession();
        var run = () => agent.RunAsync("hello", other);
        await run.Should().ThrowAsync<ArgumentException>().WithParameterName("session");
        var serialize = async () => await agent.SerializeSessionAsync(other);
        await serialize.Should().ThrowAsync<ArgumentException>().WithParameterName("session");
    }

    private sealed class OtherSession : AgentSession;

    internal sealed class History(string stateKey = "history") : ChatHistoryProvider
    {
        public override IReadOnlyList<string> StateKeys => [stateKey];
        public List<InvokedContext> Completed { get; } = [];
        protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IEnumerable<ChatMessage>>([new(ChatRole.Assistant, "remembered")]);
        protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            Completed.Add(context);
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class Context(string stateKey = "context") : AIContextProvider
    {
        public override IReadOnlyList<string> StateKeys => [stateKey];
        public List<InvokedContext> Completed { get; } = [];
        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AIContext { Instructions = "context instructions", Messages = [new(ChatRole.User, "context message")] });
        protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            Completed.Add(context);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Fixture : ICodexAppServerClientFactory, ICodexAppServerClientOptionsFactory, ICodexMcpServerClientFactory, IAsyncDisposable
    {
        private readonly CodexClient _exec = new(new CodexClientOptions());
        public CodexSdk Sdk { get; }
        public List<Rpc> Rpcs { get; } = [];
        public List<CodexAppServerClientOptions> ClientOptions { get; } = [];
        public bool AutoComplete { get; init; } = true;
        public string? FailureStage { get; init; }
        public bool ReportErrors { get; init; }
        public Fixture() => Sdk = new(_exec, this, this);
        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default) => StartAsync(_ => { }, ct);
        public Task<CodexAppServerClient> StartAsync(Action<CodexAppServerClientOptions> configure, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (FailureStage == "client") throw new IOException("client startup failed");
            var options = new CodexAppServerClientOptions();
            configure(options);
            ClientOptions.Add(options);
            var rpc = new Rpc { AutoComplete = AutoComplete, FailureStage = FailureStage, ReportErrors = ReportErrors };
            Rpcs.Add(rpc);
            return Task.FromResult(new CodexAppServerClient(options, new Lifetime(), rpc, NullLogger.Instance,
                CodexAppServerClient.CreateDefaultSerializerOptions(), startExitWatcher: false));
        }
        Task<CodexMcpServerClient> ICodexMcpServerClientFactory.StartAsync(CancellationToken ct) => throw new NotSupportedException();
        public async ValueTask DisposeAsync() { await Sdk.DisposeAsync(); await _exec.DisposeAsync(); }
    }

    private sealed class Lifetime : IAppServerLifetime
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => null;
        public int? ExitCode => null;
        public IReadOnlyList<string> DiagnosticTail => [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Rpc : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public List<(string Method, JsonElement Parameters)> Requests { get; } = [];
        public bool AutoComplete { get; init; }
        public string? FailureStage { get; init; }
        public bool ReportErrors { get; init; }
        public bool Disposed { get; private set; }
        public async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (method == FailureStage) throw new IOException(method + " failed");
            Requests.Add((method, JsonSerializer.SerializeToElement(@params, CodexAppServerClient.CreateDefaultSerializerOptions())));
            if (method is "thread/start" or "thread/resume") return Json("""{"thread":{"id":"thread-1"}}""");
            if (method == "turn/start")
            {
                await Emit("item/agentMessage/delta", """{"threadId":"thread-1","turnId":"turn-1","itemId":"message-1","delta":"hello "}""");
                if (AutoComplete)
                {
                    await Emit("item/agentMessage/delta", """{"threadId":"thread-1","turnId":"turn-1","itemId":"message-1","delta":"world"}""");
                    if (ReportErrors)
                    {
                        await Emit("error", """{"threadId":"thread-1","turnId":"turn-1","error":{"message":"temporary failure"},"willRetry":true}""");
                        await Emit("turn/completed", """{"threadId":"thread-1","turn":{"id":"turn-1","status":"failed","error":{"message":"terminal failure"}}}""");
                    }
                    else await Emit("turn/completed", """{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}""");
                }
                return Json("""{"turn":{"id":"turn-1","status":"inProgress"}}""");
            }
            return Json("{}");
        }
        private ValueTask Emit(string method, string json) => OnNotification?.Invoke(new(method, Json(json))) ?? ValueTask.CompletedTask;
        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        private static JsonElement Json(string text) { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    }
}
