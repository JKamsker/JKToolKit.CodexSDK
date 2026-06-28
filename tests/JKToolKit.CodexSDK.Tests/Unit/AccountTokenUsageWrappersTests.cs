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
    public async Task ReadAccountRateLimitsAsync_ParsesRateLimitResetCredits()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            rateLimits = new
            {
                primary = new
                {
                    usedPercent = 25
                }
            },
            rateLimitResetCredits = new
            {
                availableCount = 2L
            }
        });
        var rpc = new FakeRpc
        {
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountRateLimitsAsync();

        rpc.LastMethod.Should().Be("account/rateLimits/read");
        result.RateLimitResetCredits.Should().NotBeNull();
        result.RateLimitResetCredits!.AvailableCount.Should().Be(2);
        result.RateLimitResetCredits.Raw.GetProperty("availableCount").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task ConsumeAccountRateLimitResetCreditAsync_CallsExpectedMethod_AndParsesOutcome()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            outcome = "alreadyRedeemed"
        });
        var rpc = new FakeRpc
        {
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ConsumeAccountRateLimitResetCreditAsync("attempt-1");

        rpc.LastMethod.Should().Be("account/rateLimitResetCredit/consume");
        JsonSerializer.Serialize(rpc.LastParams, CodexAppServerClient.CreateDefaultSerializerOptions())
            .Should().Contain("\"idempotencyKey\":\"attempt-1\"");
        result.Outcome.Should().Be("alreadyRedeemed");
        result.OutcomeKind.Should().Be(AccountRateLimitResetCreditConsumeOutcome.AlreadyRedeemed);
    }

    [Fact]
    public async Task ReadWorkspaceMessagesAsync_CallsExpectedMethod_AndParsesMessages()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            featureEnabled = true,
            messages = new[]
            {
                new
                {
                    messageId = "msg-1",
                    messageType = "headline",
                    messageBody = "Notice",
                    createdAt = 100L,
                    archivedAt = (long?)null
                }
            }
        });
        var rpc = new FakeRpc
        {
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadWorkspaceMessagesAsync();

        rpc.LastMethod.Should().Be("account/workspaceMessages/read");
        result.FeatureEnabled.Should().BeTrue();
        result.Messages.Should().ContainSingle();
        result.Messages[0].MessageId.Should().Be("msg-1");
        result.Messages[0].MessageKind.Should().Be(WorkspaceMessageKind.Headline);
        result.Messages[0].CreatedAt.Should().Be(100);
    }

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
