using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging.Abstractions;
using AppServerReviewTarget = JKToolKit.CodexSDK.AppServer.ReviewTarget;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSdkReviewRoutingTests
{
    [Fact]
    public async Task ReviewAsync_ExecMode_RoutesToExecFacade()
    {
        var exec = new FakeExecClient
        {
            Result = new CodexReviewResult(0, "ok", string.Empty)
        };

        var sdk = new CodexSdk(exec, new FakeAppServerFactory(throwOnStart: true), new FakeMcpFactory());

        var options = new CodexReviewOptions(Directory.GetCurrentDirectory())
        {
            Uncommitted = true
        };

        var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
        {
            Mode = CodexSdkReviewMode.Exec,
            Exec = options
        });

        routed.Mode.Should().Be(CodexSdkReviewMode.Exec);
        routed.Exec.Should().NotBeNull();
        routed.Exec!.ExitCode.Should().Be(0);
        exec.ReviewCalls.Should().Be(1);
    }

    [Fact]
    public async Task ReviewAsync_AppServerMode_StartsThread_AndStartsReview()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("thread/start", JsonSerializer.SerializeToElement(new { id = "thr_1" }));
        rpc.EnqueueResult("review/start", JsonSerializer.SerializeToElement(new
        {
            reviewThreadId = "thr_1",
            turn = new { id = "turn_1", threadId = "thr_1" }
        }));

        await using var appClient = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(appClient), new FakeMcpFactory());

        var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
        {
            Mode = CodexSdkReviewMode.AppServer,
            AppServer = new CodexSdkAppServerReviewOptions
            {
                Thread = new ThreadStartOptions
                {
                    Cwd = Directory.GetCurrentDirectory(),
                    Model = CodexModel.Gpt52Codex
                },
                Delivery = ReviewDelivery.Inline,
                Target = new AppServerReviewTarget.UncommittedChanges()
            }
        });

        routed.Mode.Should().Be(CodexSdkReviewMode.AppServer);
        routed.AppServer.Should().NotBeNull();
        routed.AppServer!.Thread.Id.Should().Be("thr_1");
        routed.AppServer.SourceThread.Id.Should().Be("thr_1");
        routed.AppServer.Review.Turn.TurnId.Should().Be("turn_1");

        await routed.DisposeAsync();
        await routed.DisposeAsync();
        rpc.AssertDrained();
    }

    [Fact]
    public async Task ReviewAsync_AppServerDetachedMode_ResolvesDetachedReviewThread()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("thread/start", JsonSerializer.SerializeToElement(new { id = "thr_bootstrap" }));
        rpc.EnqueueResult("review/start", JsonSerializer.SerializeToElement(new
        {
            reviewThreadId = "thr_review",
            turn = new { id = "turn_1", threadId = "thr_bootstrap" }
        }));
        rpc.EnqueueResult("thread/resume", JsonSerializer.SerializeToElement(new { id = "thr_review" }));

        await using var appClient = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(appClient), new FakeMcpFactory());

        var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
        {
            Mode = CodexSdkReviewMode.AppServer,
            AppServer = new CodexSdkAppServerReviewOptions
            {
                Thread = new ThreadStartOptions
                {
                    Cwd = Directory.GetCurrentDirectory(),
                    Model = CodexModel.Gpt52Codex
                },
                Delivery = ReviewDelivery.Detached,
                Target = new AppServerReviewTarget.UncommittedChanges()
            }
        });

        routed.AppServer.Should().NotBeNull();
        routed.AppServer!.Thread.Id.Should().Be("thr_review");
        routed.AppServer.SourceThread.Id.Should().Be("thr_bootstrap");
        routed.AppServer.Review.ReviewThreadId.Should().Be("thr_review");

        await routed.DisposeAsync();
        await routed.DisposeAsync();
        rpc.AssertDrained();
    }

    [Fact]
    public async Task ReviewAsync_AppServerMode_CanUseExistingSourceThread()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("thread/resume", JsonSerializer.SerializeToElement(new { id = "thr_existing" }));
        rpc.EnqueueResult("review/start", JsonSerializer.SerializeToElement(new
        {
            reviewThreadId = "thr_existing",
            turn = new { id = "turn_1", threadId = "thr_existing" }
        }));

        await using var appClient = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(appClient), new FakeMcpFactory());

        var routed = await sdk.ReviewAsync(new CodexSdkReviewOptions
        {
            Mode = CodexSdkReviewMode.AppServer,
            AppServer = new CodexSdkAppServerReviewOptions
            {
                ExistingThreadId = "thr_existing",
                Delivery = ReviewDelivery.Inline,
                Target = new AppServerReviewTarget.UncommittedChanges()
            }
        });

        routed.AppServer.Should().NotBeNull();
        routed.AppServer!.Thread.Id.Should().Be("thr_existing");
        routed.AppServer.SourceThread.Id.Should().Be("thr_existing");
        routed.AppServer.Review.Turn.TurnId.Should().Be("turn_1");

        await routed.DisposeAsync();
        await routed.DisposeAsync();
        rpc.AssertDrained();
    }

    [Fact]
    public async Task ReviewAsync_AppServerMode_Throws_WhenThreadSourceIsAmbiguous()
    {
        var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(throwOnStart: true), new FakeMcpFactory());

        var act = async () => await sdk.ReviewAsync(new CodexSdkReviewOptions
        {
            Mode = CodexSdkReviewMode.AppServer,
            AppServer = new CodexSdkAppServerReviewOptions
            {
                ExistingThreadId = "thr_existing",
                Thread = new ThreadStartOptions
                {
                    Cwd = Directory.GetCurrentDirectory(),
                    Model = CodexModel.Gpt52Codex
                },
                Target = new AppServerReviewTarget.UncommittedChanges()
            }
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one of Thread or ExistingThreadId must be provided.*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReviewFailure_DisposesAcquiredClientAndPreservesOriginalFailure(bool factoryFailure)
    {
        var rpc = new SequencedRpc { Failure = new IOException("review connection lost") };
        await using var client = new CodexAppServerClient(new(), new FakeProcess(), rpc, NullLogger.Instance, startExitWatcher: false);
        await using var sdk = new CodexSdk(new FakeExecClient(), factoryFailure ? new FakeAppServerFactory(true) : new FakeAppServerFactory(client), new FakeMcpFactory());
        var options = new CodexSdkAppServerReviewOptions { ExistingThreadId = "thread", Target = new AppServerReviewTarget.UncommittedChanges() };
        if (factoryFailure) await Assert.ThrowsAsync<NotSupportedException>(() => sdk.ReviewAppServerAsync(options));
        else
        {
            var ex = await Assert.ThrowsAsync<IOException>(() => sdk.ReviewAppServerAsync(options));
            Assert.Same(rpc.Failure, ex);
            Assert.Equal(1, rpc.Disposals);
        }
    }

    [Fact]
    public async Task ReviewRouting_RejectsUnknownModeAndMissingModeOptions()
    {
        await using var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(true), new FakeMcpFactory());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sdk.ReviewAsync(new() { Mode = (CodexSdkReviewMode)99 }));
        await Assert.ThrowsAsync<ArgumentException>(() => sdk.ReviewAsync(new() { Mode = CodexSdkReviewMode.Exec }));
        await Assert.ThrowsAsync<ArgumentException>(() => sdk.ReviewAsync(new() { Mode = CodexSdkReviewMode.AppServer }));
    }

    [Fact]
    public async Task OwnedSdk_DisposesSynchronousExecOnlyOnce()
    {
        var exec = new FakeExecClient();
        var sdk = CodexSdk.CreateOwned(exec, new FakeAppServerFactory(true), new FakeMcpFactory());
        await sdk.DisposeAsync();
        await sdk.DisposeAsync();
        Assert.Equal(1, exec.Disposals);
    }

    [Fact]
    public async Task Runtime_ServerMetadataRetainsCapabilitiesAndDoesNotRefreshAccount()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("initialize", JsonSerializer.SerializeToElement(new { userAgent = "codex/0.160.0", capabilities = new { feature = true } }));
        rpc.EnqueueResult("account/read", JsonSerializer.SerializeToElement(new { account = (object?)null, requiresOpenaiAuth = false }));
        await using var client = new CodexAppServerClient(new(), new FakeProcess(), rpc, NullLogger.Instance, startExitWatcher: false);
        await client.InitializeAsync(new("test", "Test", "1"));
        await using var sdk = new CodexSdk(new FakeExecClient(), new FakeAppServerFactory(client), new FakeMcpFactory());
        var result = await sdk.Runtime.GetInfoAsync();
        Assert.Same(client.InitializeResult, result.Initialize);
        Assert.True(result.Capabilities!.Value.GetProperty("feature").GetBoolean());
        Assert.NotNull(result.Account);
        Assert.False(result.Account.RequiresOpenaiAuth);
        Assert.False(rpc.LastParams.GetProperty("refreshToken").GetBoolean());
        rpc.AssertDrained();
    }

    private sealed class FakeExecClient : ICodexClient
    {
        public int ReviewCalls { get; private set; }
        public CodexReviewResult Result { get; init; } = new(0, string.Empty, string.Empty);

        public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default)
        {
            ReviewCalls++;
            return Task.FromResult(Result);
        }

        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int Disposals { get; private set; }
        public void Dispose() { Disposals++; }
    }

    private sealed class FakeAppServerFactory : ICodexAppServerClientFactory
    {
        private readonly CodexAppServerClient? _client;
        private readonly bool _throwOnStart;

        public FakeAppServerFactory(CodexAppServerClient client)
        {
            _client = client;
            _throwOnStart = false;
        }

        public FakeAppServerFactory(bool throwOnStart)
        {
            _throwOnStart = throwOnStart;
        }

        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default)
        {
            if (_throwOnStart)
            {
                throw new NotSupportedException();
            }

            return Task.FromResult(_client ?? throw new InvalidOperationException("Client not provided."));
        }
    }

    private sealed class FakeMcpFactory : ICodexMcpServerClientFactory
    {
        public Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SequencedRpc : IJsonRpcConnection
    {
        private readonly Queue<(string Method, JsonElement Result)> _results = new();
        public Exception? Failure { get; init; }
        public int Disposals { get; private set; }
        public JsonElement LastParams { get; private set; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public void EnqueueResult(string method, JsonElement result) => _results.Enqueue((method, result));

        public void AssertDrained() =>
            _results.Should().BeEmpty("all enqueued RPC results should have been consumed");

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (Failure is not null) throw Failure;
            LastParams = JsonSerializer.SerializeToElement(@params, CodexAppServerClient.CreateDefaultSerializerOptions());
            _results.Should().NotBeEmpty();
            var (expectedMethod, result) = _results.Dequeue();
            method.Should().Be(expectedMethod);
            return Task.FromResult(result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
