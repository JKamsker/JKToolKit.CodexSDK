using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerFuzzyFileSearchClientTests
{
    [Fact]
    public async Task StartFuzzyFileSearchSessionAsync_RejectsEmptyRoots()
    {
        await using var client = CreateClient(experimentalApi: true);

        var act = async () => await client.StartFuzzyFileSearchSessionAsync("session-1", Array.Empty<string>());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Roots cannot be empty*");
    }

    [Fact]
    public async Task StartFuzzyFileSearchSessionAsync_RejectsWhitespaceRoots()
    {
        await using var client = CreateClient(experimentalApi: true);

        var act = async () => await client.StartFuzzyFileSearchSessionAsync("session-1", ["C:\\repo", " "]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Roots cannot contain*");
    }

    private static CodexAppServerClient CreateClient(bool experimentalApi) =>
        new(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi },
            new FakeProcess(),
            new FakeRpc(),
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

    private sealed class FakeRpc : IJsonRpcConnection
    {
#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            Task.FromResult(JsonDocument.Parse("""{}""").RootElement.Clone());

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
