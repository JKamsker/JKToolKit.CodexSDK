using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AccountTokenUsageWrappersTests
{
    [Fact]
    public async Task ReadAccountTokenUsageAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            summary = new
            {
                lifetimeTokens = 1234L,
                peakDailyTokens = 500L,
                longestRunningTurnSec = 42L,
                currentStreakDays = 3L,
                longestStreakDays = 8L
            },
            dailyUsageBuckets = new[]
            {
                new
                {
                    startDate = "2026-06-09",
                    tokens = 321L
                }
            }
        });
        var rpc = new FakeRpc
        {
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountTokenUsageAsync();

        rpc.LastMethod.Should().Be("account/usage/read");
        rpc.LastParams.Should().BeNull();
        result.Summary.LifetimeTokens.Should().Be(1234);
        result.Summary.PeakDailyTokens.Should().Be(500);
        result.Summary.LongestRunningTurnSec.Should().Be(42);
        result.Summary.CurrentStreakDays.Should().Be(3);
        result.Summary.LongestStreakDays.Should().Be(8);
        result.DailyUsageBuckets.Should().ContainSingle();
        result.DailyUsageBuckets![0].StartDate.Should().Be("2026-06-09");
        result.DailyUsageBuckets[0].Tokens.Should().Be(321);
        result.Raw.GetProperty("summary").GetProperty("lifetimeTokens").GetInt64().Should().Be(1234);
    }

    [Fact]
    public async Task ReadAccountTokenUsageAsync_AllowsMissingDailyBuckets()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            summary = new
            {
                lifetimeTokens = (long?)null,
                peakDailyTokens = (long?)null,
                longestRunningTurnSec = (long?)null,
                currentStreakDays = (long?)null,
                longestStreakDays = (long?)null
            }
        });
        var rpc = new FakeRpc
        {
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountTokenUsageAsync();

        result.Summary.LifetimeTokens.Should().BeNull();
        result.DailyUsageBuckets.Should().BeNull();
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc) =>
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

    private sealed class FakeRpc : IJsonRpcConnection
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
