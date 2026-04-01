using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AccountLoginWrappersTests
{
    [Fact]
    public async Task StartAccountLoginAsync_CallsExpectedMethod_ForApiKey()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            type = "apiKey"
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/start",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("type").GetString().Should().Be("apiKey");
                json.GetProperty("apiKey").GetString().Should().Be("sk-test");
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.StartAccountLoginAsync(new AccountLoginStartOptions.ApiKey("sk-test"));

        result.Should().BeOfType<AccountLoginStartResult.ApiKey>();
    }

    [Fact]
    public async Task StartAccountLoginAsync_ParsesDeviceCodeFlow()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            type = "chatgptDeviceCode",
            loginId = "login_123",
            verificationUrl = "https://example.com/device",
            userCode = "ABCD-EFGH"
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/start",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("type").GetString().Should().Be("chatgptDeviceCode");
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.StartAccountLoginAsync(new AccountLoginStartOptions.ChatGptDeviceCode());

        var typed = result.Should().BeOfType<AccountLoginStartResult.ChatGptDeviceCode>().Subject;
        typed.LoginId.Should().Be("login_123");
        typed.VerificationUrl.Should().Be("https://example.com/device");
        typed.UserCode.Should().Be("ABCD-EFGH");
    }

    [Fact]
    public async Task CancelAccountLoginAsync_CallsExpectedMethod_AndParsesStatus()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            status = "canceled"
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/cancel",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("loginId").GetString().Should().Be("login_123");
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.CancelAccountLoginAsync("login_123");

        result.Status.Should().Be(AccountLoginCancelStatus.Canceled);
    }

    [Fact]
    public async Task CancelAccountLoginAsync_UnknownStatus_Throws()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/cancel",
            Result = JsonSerializer.SerializeToElement(new
            {
                status = "paused"
            })
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.CancelAccountLoginAsync("login_123");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unknown status*");
    }

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

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            method.Should().Be(AssertMethod);
            AssertParams?.Invoke(@params);
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
