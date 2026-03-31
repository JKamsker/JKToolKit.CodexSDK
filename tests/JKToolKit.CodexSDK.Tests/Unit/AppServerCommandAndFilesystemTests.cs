using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerCommandAndFilesystemTests
{
    [Fact]
    public async Task CommandExecAsync_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse("""{"exitCode":0,"stdout":"ok","stderr":""}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["git", "status"],
            Cwd = "C:\\repo",
            ProcessId = "proc-1",
            StreamStdoutStderr = true
        });

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("ok");
        rpc.LastMethod.Should().Be("command/exec");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"processId\":\"proc-1\"");
    }

    [Fact]
    public async Task CommandExecAsync_RequiresProcessId_WhenStreaming()
    {
        await using var client = CreateClient(new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement });

        var act = async () => await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["cmd"],
            StreamStdoutStderr = true
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ProcessId*required*");
    }

    [Fact]
    public async Task FsWatchAsync_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse("""{"path":"C:\\repo","watchId":"watch-1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.FsWatchAsync(new FsWatchOptions { Path = "C:\\repo" });

        result.WatchId.Should().Be("watch-1");
        result.Path.Should().Be("C:\\repo");
        rpc.LastMethod.Should().Be("fs/watch");
    }

    [Fact]
    public async Task FsUnwatchAsync_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        await client.FsUnwatchAsync(new FsUnwatchOptions { WatchId = "watch-1" });

        rpc.LastMethod.Should().Be("fs/unwatch");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"watchId\":\"watch-1\"");
    }

    private static CodexAppServerClient CreateClient(RecordingRpc rpc) =>
        new(
            new CodexAppServerClientOptions(),
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

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
