using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;
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

    [Fact]
    public async Task UpdateFuzzyFileSearchSessionAsync_AllowsWhitespaceQuery()
    {
        await using var client = CreateClient(experimentalApi: true);

        await client.UpdateFuzzyFileSearchSessionAsync("session-1", "   ");
    }

    [Fact]
    public async Task FuzzyFileSearchAsync_SendsTypedRequest()
    {
        using var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("fuzzyFileSearch");
                var typed = @params.Should().BeOfType<FuzzyFileSearchParams>().Which;
                typed.Query.Should().Be("abc");
                typed.Roots.Should().Equal(new[] { "C:\\repo" });
                typed.CancellationToken.Should().Be("token");

                using var doc = JsonDocument.Parse("""{"files":[{"root":"C:\\repo","path":"C:\\repo\\Program.cs","fileName":"Program.cs","score":100,"matchType":"file"}]}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        await using var client = CreateClient(experimentalApi: true, rpc: rpc);

        var results = await client.FuzzyFileSearchAsync("abc", ["C:\\repo"], cancellationToken: "token");

        results.Should().ContainSingle()
            .Which.MatchKind.Should().Be(FuzzyFileSearchMatchType.File);
    }

    private static CodexAppServerClient CreateClient(bool experimentalApi, FakeRpc? rpc = null)
    {
        rpc ??= new FakeRpc();
        return new(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);
    }

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

        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            SendRequestAsyncImpl?.Invoke(method, @params, ct) ?? Task.FromResult(JsonDocument.Parse("""{}""").RootElement.Clone());

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
