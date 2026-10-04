using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.StructuredOutputs;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredOutputBehaviorTests
{
    public sealed record Answer(string Text);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exec_StartAndResume_CloneOptionsAndReturnSourceMetadata(bool resume)
    {
        await using var client = new ScriptedExec("prefix ```json\n{\"text\":\"answer\"}\n``` suffix");
        var options = new CodexSessionOptions(Path.GetTempPath(), "original");
        var facade = new CodexExecFacade(client);
        var result = resume
            ? await facade.RunStructuredAsync<Answer>(SessionId.Parse("session"), options)
            : await facade.RunStructuredAsync<Answer>(options);
        Assert.Equal("answer", result.Value.Text);
        Assert.Equal("{\"text\":\"answer\"}", result.RawJson);
        Assert.Contains("prefix", result.RawText);
        Assert.Equal("session", result.SessionId);
        Assert.Equal(client.LogPath, result.LogPath);
        Assert.Null(options.OutputSchema);
        Assert.NotSame(options, Assert.Single(client.Calls).Options);
        Assert.NotNull(client.Calls[0].Options.OutputSchema);
        Assert.All(client.Handles, h => Assert.True(h.Disposed));
        if (resume) Assert.Equal(8, client.Handles[^1].StreamOptions!.FromByteOffset);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Exec_Retry_ReportsAttemptsAndRetainsContext(bool resume, bool withProgress)
    {
        await using var client = new ScriptedExec("{\"text\":42}", "{\"text\":\"fixed\"}");
        var options = new CodexSessionOptions(Path.GetTempPath(), "original");
        var attempts = new List<(int, int, CodexStructuredAttemptKind)>();
        var located = new List<string?>();
        var events = new List<CodexEvent>();
        var failures = new List<int>();
        var progress = new CodexStructuredRunProgress
        {
            AttemptStarting = (a, n, k) => attempts.Add((a, n, k)),
            SessionLocated = (id, path) => located.Add(path),
            EventReceived = events.Add,
            ParseFailed = (a, ex) => failures.Add(a)
        };
        CodexStructuredRetryContext? context = null;
        var retry = new CodexStructuredRetryOptions { MaxAttempts = 2, RetryPromptFactory = c => { context = c; return "repair"; } };
        var facade = new CodexExecFacade(client);
        var id = SessionId.Parse("session");
        var result = (resume, withProgress) switch
        {
            (false, false) => await facade.RunStructuredWithRetryAsync<Answer>(options, retry),
            (false, true) => await facade.RunStructuredWithRetryAsync<Answer>(options, progress, retry),
            (true, false) => await facade.RunStructuredWithRetryAsync<Answer>(id, options, retry),
            _ => await facade.RunStructuredWithRetryAsync<Answer>(id, options, progress, retry)
        };
        Assert.Equal("fixed", result.Value.Text);
        Assert.Equal(new[] { "original", "repair" }, client.Calls.Select(c => c.Options.Prompt));
        Assert.Equal(resume, client.Calls[0].Resume);
        Assert.True(client.Calls[1].Resume);
        Assert.Equal("original", options.Prompt);
        Assert.NotNull(context);
        Assert.Equal(1, context.Attempt);
        Assert.Equal(2, context.MaxAttempts);
        Assert.Equal(id, context.SessionId);
        Assert.Equal(client.LogPath, context.LogPath);
        Assert.Null(context.ThreadId);
        Assert.Equal("{\"text\":42}", context.RawText);
        Assert.Equal(context.RawText, context.ExtractedJson);
        Assert.IsType<CodexStructuredOutputParseException>(context.Exception);
        if (withProgress)
        {
            Assert.Equal(new[] { (1, 2, resume ? CodexStructuredAttemptKind.Resume : CodexStructuredAttemptKind.Start), (2, 2, CodexStructuredAttemptKind.Resume) }, attempts);
            Assert.Equal(new[] { client.LogPath, client.LogPath }, located);
            Assert.Equal(new[] { 1 }, failures);
            Assert.Equal(2, events.Count);
        }
        Assert.All(client.Handles, h => Assert.True(h.Disposed));
    }

    [Fact]
    public async Task Exec_EmptyFinalMessage_IsParseFailureAndDisposesSession()
    {
        await using var client = new ScriptedExec(" ");
        var ex = await Assert.ThrowsAsync<CodexStructuredOutputParseException>(() => client.RunStructuredAsync<Answer>(new(Path.GetTempPath(), "prompt")));
        Assert.Equal("", ex.RawText);
        Assert.Null(ex.ExtractedJson);
        Assert.True(Assert.Single(client.Handles).Disposed);
    }

    [Fact]
    public async Task AppServer_Retry_FirstAttemptSuccessUsesCustomSerializer()
    {
        var rpc = new ScriptedRpc(false, "{\"Text\":\"answer\"}");
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        var result = await client.RunTurnStructuredWithRetryAsync<Answer>("t", new() { Input = [TurnInputItem.Text("original")] },
            structured: new() { SerializerOptions = new JsonSerializerOptions(), TolerantJsonExtraction = false });
        Assert.Equal("answer", result.Value.Text);
        Assert.Equal("t", result.ThreadId);
        Assert.Equal("u1", result.TurnId);
        Assert.Single(rpc.Requests);
        Assert.True(rpc.Requests[0].GetProperty("outputSchema").GetProperty("properties").TryGetProperty("Text", out _));
    }

    [Fact]
    public async Task Exec_ExhaustedRetries_PreservesParseFailureAndSession()
    {
        await using var client = new ScriptedExec("broken", "null");
        var ex = await Assert.ThrowsAsync<CodexStructuredOutputRetryFailedException>(() => client.RunStructuredWithRetryAsync<Answer>(
            new(Path.GetTempPath(), "original"), new CodexStructuredRetryOptions { MaxAttempts = 2 }));
        Assert.Equal(2, ex.Attempts);
        Assert.Equal("session", ex.SessionId);
        Assert.Equal(client.LogPath, ex.LogPath);
        var parse = Assert.IsType<CodexStructuredOutputParseException>(ex.InnerException);
        Assert.Equal("null", parse.RawText);
        Assert.Equal("null", parse.ExtractedJson);
        Assert.IsType<InvalidOperationException>(parse.InnerException);
        Assert.All(client.Handles, h => Assert.True(h.Disposed));
    }

    [Fact]
    public async Task Exec_CancellationFromProgress_DoesNotStartAnotherAttempt()
    {
        await using var client = new ScriptedExec("broken");
        using var cancellation = new CancellationTokenSource();
        var progress = new CodexStructuredRunProgress { ParseFailed = (_, _) => cancellation.Cancel() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RunStructuredWithRetryAsync<Answer>(new(Path.GetTempPath(), "original"), progress, ct: cancellation.Token));
        Assert.Single(client.Calls);
        Assert.True(Assert.Single(client.Handles).Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppServer_UsesFinalItemOrDeltasAndClonesInput(bool deltasOnly)
    {
        var rpc = new ScriptedRpc(deltasOnly, "{\"text\":\"answer\"}");
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        var options = new TurnStartOptions { Input = [TurnInputItem.Text("original")] };
        var result = await client.RunTurnStructuredAsync<Answer>("t", options);
        Assert.Equal("answer", result.Value.Text);
        Assert.Equal("t", result.ThreadId);
        Assert.Equal("u1", result.TurnId);
        Assert.Null(result.SessionId);
        Assert.Null(options.OutputSchema);
        Assert.Equal("object", Assert.Single(rpc.Requests).GetProperty("outputSchema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AppServer_Retry_UsesCustomPromptAndPreservesOriginalInput()
    {
        var rpc = new ScriptedRpc(false, "{\"text\":1}", "{\"text\":\"fixed\"}");
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        CodexStructuredRetryContext? context = null;
        var options = new TurnStartOptions { Input = [TurnInputItem.Text("original")] };
        var result = await client.RunTurnStructuredWithRetryAsync<Answer>("t", options,
            new CodexStructuredRetryOptions { RetryPromptFactory = c => { context = c; return "repair"; } });
        Assert.Equal("fixed", result.Value.Text);
        Assert.Equal("u2", result.TurnId);
        Assert.Equal("repair", rpc.Requests[1].GetProperty("input")[0].GetProperty("text").GetString());
        Assert.Equal("original", rpc.Requests[0].GetProperty("input")[0].GetProperty("text").GetString());
        Assert.Equal("t", context!.ThreadId);
        Assert.Null(context.SessionId);
        Assert.Null(context.TurnId);
        Assert.Null(context.LogPath);
        Assert.Equal(1, context.Attempt);
        Assert.Equal("{\"text\":1}", context.ExtractedJson);
        Assert.Null(options.OutputSchema);
    }

    [Theory]
    [InlineData("")]
    [InlineData("broken")]
    [InlineData("null")]
    public async Task AppServer_ExhaustionReturnsLastParseFailure(string text)
    {
        var rpc = new ScriptedRpc(true, text, text);
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        var ex = await Assert.ThrowsAsync<CodexStructuredOutputParseException>(() => client.RunTurnStructuredWithRetryAsync<Answer>("t",
            new() { Input = [TurnInputItem.Text("original")] }, new() { MaxAttempts = 2 }));
        Assert.Equal(text, ex.RawText);
        Assert.Equal(2, rpc.Requests.Count);
    }

    [Fact]
    public async Task AppServer_CancellationWhileCapturing_DoesNotRetry()
    {
        var rpc = new ScriptedRpc(true, "partial") { CompleteTurn = false };
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        using var cancellation = new CancellationTokenSource();
        var task = client.RunTurnStructuredWithRetryAsync<Answer>("t", new() { Input = [TurnInputItem.Text("original")] }, ct: cancellation.Token);
        Assert.Single(rpc.Requests);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(rpc.Requests);
    }

    [Fact]
    public async Task InvalidAttemptsAndCanceledCalls_NeverReachTransport()
    {
        await using var exec = new ScriptedExec();
        var options = new CodexSessionOptions(Path.GetTempPath(), "original");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => exec.RunStructuredWithRetryAsync<Answer>(options, new CodexStructuredRetryOptions { MaxAttempts = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => exec.RunStructuredAsync<Answer>(default(SessionId), options));
        await Assert.ThrowsAsync<ArgumentException>(() => exec.RunStructuredWithRetryAsync<Answer>(default(SessionId), options));
        await Assert.ThrowsAsync<ArgumentException>(() => exec.RunStructuredWithRetryAsync<Answer>(default(SessionId), options, new CodexStructuredRunProgress()));
        var rpc = new ScriptedRpc(false);
        await using var fixture = new HighLevelTurnTests.Fixture(connection: rpc);
        await using var client = await fixture.StartAsync();
        var turn = new TurnStartOptions { Input = [TurnInputItem.Text("original")] };
        await Assert.ThrowsAsync<ArgumentException>(() => client.RunTurnStructuredAsync<Answer>(" ", turn));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RunTurnStructuredWithRetryAsync<Answer>(" ", turn));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.RunTurnStructuredWithRetryAsync<Answer>("t", turn, new() { MaxAttempts = -1 }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RunTurnStructuredWithRetryAsync<Answer>("t", turn, ct: cancellation.Token));
        Assert.Empty(exec.Calls);
        Assert.Empty(rpc.Requests);
    }

    private sealed class ScriptedRpc(bool deltasOnly, params string[] outputs) : IJsonRpcConnection
    {
        public List<JsonElement> Requests { get; } = [];
        public bool CompleteTurn { get; init; } = true;
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct)
        {
            if (method != "turn/start") throw new InvalidOperationException(method);
            Requests.Add(JsonSerializer.SerializeToElement(parameters, CodexAppServerClient.CreateDefaultSerializerOptions()));
            var id = $"u{Requests.Count}";
            var text = outputs[Requests.Count - 1];
            await Emit("item/agentMessage/delta", new { threadId = "t", turnId = id, itemId = "a", delta = deltasOnly ? text : "draft" });
            await Emit("item/completed", new { threadId = "t", turnId = id, item = new { id = "tool", type = "commandExecution" } });
            if (!deltasOnly) await Emit("item/completed", new { threadId = "t", turnId = id, item = new { id = "a", type = "agentMessage", text } });
            if (CompleteTurn) await Emit("turn/completed", new { threadId = "t", turn = new { id, status = "completed" } });
            return JsonSerializer.SerializeToElement(new { turn = new { id } });
        }
        private ValueTask Emit(string method, object payload) => OnNotification?.Invoke(new(method, JsonSerializer.SerializeToElement(payload))) ?? ValueTask.CompletedTask;
        public Task SendNotificationAsync(string method, object? parameters, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScriptedExec(params string[] outputs) : ICodexClient
    {
        public string LogPath { get; } = Path.GetTempFileName();
        public List<(bool Resume, CodexSessionOptions Options)> Calls { get; } = [];
        public List<Handle> Handles { get; } = [];
        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken = default) => Run(false, options, cancellationToken);
        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId id, CodexSessionOptions options, CancellationToken cancellationToken = default) => Run(true, options, cancellationToken);
        private Task<ICodexSessionHandle> Run(bool resume, CodexSessionOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var output = outputs[Calls.Count];
            Calls.Add((resume, options));
            return Task.FromResult<ICodexSessionHandle>(Add(output));
        }
        private Handle Add(string output) { var handle = new Handle(LogPath, output); Handles.Add(handle); return handle; }
        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId id, CancellationToken cancellationToken = default)
        {
            File.WriteAllText(LogPath, "history\n");
            return Task.FromResult<ICodexSessionHandle>(Add("historical"));
        }
        public Task<ICodexSessionHandle> AttachToLogAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() => File.Delete(LogPath);
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class Handle(string logPath, string output) : ICodexSessionHandle
    {
        public CodexSessionInfo Info { get; } = new(SessionId.Parse("session"), logPath, DateTimeOffset.UtcNow, Path.GetTempPath(), null);
        public bool Disposed { get; private set; }
        public EventStreamOptions? StreamOptions { get; private set; }
        public bool IsLive => false;
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public async IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            StreamOptions = options;
            cancellationToken.ThrowIfCancellationRequested();
            yield return new TaskCompleteEvent { Type = "task_complete", Timestamp = DateTimeOffset.UtcNow, RawPayload = JsonSerializer.SerializeToElement(new { }), LastAgentMessage = output };
            await Task.CompletedTask;
        }
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> ExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public IDisposable OnExit(Action<int> callback) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
