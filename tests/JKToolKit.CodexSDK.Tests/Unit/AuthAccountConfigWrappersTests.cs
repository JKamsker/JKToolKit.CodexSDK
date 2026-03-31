using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AuthAccountConfigWrappersTests
{
    [Fact]
    public async Task ReadAccountAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            account = new
            {
                type = "chatgpt",
                email = "person@example.test",
                planType = "plus"
            },
            requiresOpenaiAuth = false
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/read",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("refreshToken").GetBoolean().Should().BeTrue();
            },
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountAsync(new AccountReadOptions
        {
            RefreshToken = true
        });

        result.RequiresOpenaiAuth.Should().BeFalse();
        result.Account.Should().NotBeNull();
        result.Account!.Value.GetProperty("type").GetString().Should().Be("chatgpt");
        result.Account!.Value.GetProperty("planType").GetString().Should().Be("plus");
    }

    [Fact]
    public async Task ReadAccountRateLimitsAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            rateLimits = new
            {
                limitId = "codex",
                planType = "pro"
            },
            rateLimitsByLimitId = new
            {
                codex = new { limitId = "codex" },
                secondary = new { limitId = "secondary" }
            }
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/rateLimits/read",
            AssertParams = p => p.Should().BeNull(),
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountRateLimitsAsync();

        result.RateLimits.GetProperty("limitId").GetString().Should().Be("codex");
        result.RateLimits.GetProperty("planType").GetString().Should().Be("pro");
        result.RateLimitsByLimitId.Should().NotBeNull();
        result.RateLimitsByLimitId!.Should().ContainKey("codex");
        result.RateLimitsByLimitId!["secondary"].GetProperty("limitId").GetString().Should().Be("secondary");
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_TypedModeAndCwd_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "windowsSandbox/setupStart",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("mode").GetString().Should().Be("elevated");
                json.GetProperty("cwd").GetString().Should().Be("C:/repo");
            },
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var started = await client.StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode.Elevated, cwd: "C:/repo");

        started.Should().BeTrue();
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_StringOverload_RemainsCompatible()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "windowsSandbox/setupStart",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("mode").GetString().Should().Be("unelevated");
            },
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var started = await client.StartWindowsSandboxSetupAsync("unelevated");

        started.Should().BeTrue();
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_RelativeCwd_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.StartWindowsSandboxSetupAsync(
            new WindowsSandboxSetupStartOptions(WindowsSandboxSetupMode.Elevated)
            {
                Cwd = "relative\\repo"
            });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_NameSelector_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "skills/config/write",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("enabled").GetBoolean().Should().BeTrue();
                json.GetProperty("name").GetString().Should().Be("my-skill");
                json.TryGetProperty("path", out _).Should().BeFalse();
            },
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var result = await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Name = "my-skill"
        });

        result.EffectiveEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_WhenBothSelectorsProvided_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Name = "my-skill",
            Path = "skills/my-skill"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one of Path or Name*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_PathSelector_RequiresAbsolutePath()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Path = "skills\\my-skill"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAccountLoginAsync_ChatGptAuthTokens_ThrowsWhenExperimentalApiDisabled()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { type = "chatgptAuthTokens" })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.StartAccountLoginAsync(new AccountLoginStartOptions.ChatGptAuthTokens("token", "acct_123"));

        var ex = await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        ex.Which.Descriptor.Should().Be("account/login/start.chatgptAuthTokens");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAccountLoginAsync_ChatGptAuthTokens_WorksWhenExperimentalApiEnabled()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/start",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("type").GetString().Should().Be("chatgptAuthTokens");
                json.GetProperty("accessToken").GetString().Should().Be("token");
                json.GetProperty("chatgptAccountId").GetString().Should().Be("acct_123");
                json.GetProperty("chatgptPlanType").GetString().Should().Be("plus");
            },
            Result = JsonSerializer.SerializeToElement(new { type = "chatgptAuthTokens" })
        };

        await using var client = CreateClient(rpc, new CodexAppServerClientOptions
        {
            ExperimentalApi = true
        });

        var result = await client.StartAccountLoginAsync(new AccountLoginStartOptions.ChatGptAuthTokens("token", "acct_123", "plus"));

        result.Should().BeOfType<AccountLoginStartResult.ChatGptAuthTokens>();
    }

    private static CodexAppServerClient CreateClient(FakeRpc rpc, CodexAppServerClientOptions? options = null) =>
        new(
            options ?? new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync()
        {
            _tcs.TrySetCanceled();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public string AssertMethod { get; init; } = string.Empty;

        public Action<object?>? AssertParams { get; init; }

        public JsonElement Result { get; init; }

        public int SendRequestCallCount { get; private set; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            SendRequestCallCount++;
            if (!string.IsNullOrWhiteSpace(AssertMethod))
            {
                method.Should().Be(AssertMethod);
            }

            AssertParams?.Invoke(@params);
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
