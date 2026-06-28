using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RuntimeWorkspaceRootValidationTests
{
    [Fact]
    public async Task StartThreadAsync_RejectsRelativeRuntimeWorkspaceRoots()
    {
        var rpc = new GuardRpc();
        await using var client = CreateClient(rpc);

        var act = async () => await client.StartThreadAsync(new ThreadStartOptions
        {
            RuntimeWorkspaceRoots = ["relative\\repo"]
        });

        await AssertRelativeRootRejected(act, rpc);
    }

    [Fact]
    public async Task ResumeThreadAsync_RejectsRelativeRuntimeWorkspaceRoots()
    {
        var rpc = new GuardRpc();
        await using var client = CreateClient(rpc);

        var act = async () => await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            ThreadId = "thr_1",
            RuntimeWorkspaceRoots = ["relative\\repo"]
        });

        await AssertRelativeRootRejected(act, rpc);
    }

    [Fact]
    public async Task ForkThreadAsync_RejectsRelativeRuntimeWorkspaceRoots()
    {
        var rpc = new GuardRpc();
        await using var client = CreateClient(rpc);

        var act = async () => await client.ForkThreadAsync(new ThreadForkOptions
        {
            ThreadId = "thr_1",
            RuntimeWorkspaceRoots = ["relative\\repo"]
        });

        await AssertRelativeRootRejected(act, rpc);
    }

    [Fact]
    public async Task StartTurnAsync_RejectsRelativeRuntimeWorkspaceRoots()
    {
        var rpc = new GuardRpc();
        await using var client = CreateClient(rpc);

        var act = async () => await client.StartTurnAsync("thr_1", new TurnStartOptions
        {
            RuntimeWorkspaceRoots = ["relative\\repo"]
        });

        await AssertRelativeRootRejected(act, rpc);
    }

    [Fact]
    public async Task StartTurnAsync_AllowsEnvironmentNativeRelativeCwd()
    {
        using var doc = JsonDocument.Parse("""{"turnId":"turn_1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        await client.StartTurnAsync("thr_1", new TurnStartOptions
        {
            Environments =
            [
                new TurnEnvironmentOptions
                {
                    EnvironmentId = "env-1",
                    Cwd = "relative/repo"
                }
            ]
        });

        rpc.LastMethod.Should().Be("turn/start");
        rpc.LastParams.Should().NotBeNull();
    }

    private static async Task AssertRelativeRootRejected(Func<Task> act, GuardRpc rpc)
    {
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.RequestCount.Should().Be(0);
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc) =>
        new(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => 0;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GuardRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            throw new InvalidOperationException("Relative runtime workspace roots should be rejected before JSON-RPC is called.");
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
