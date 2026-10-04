using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RuntimeServerProbeContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ServerProbe_PreservesInitializeAndAccountWithoutRefreshingCredentials(bool advertiseCapabilities)
    {
        var rpc = new ProbeRpc(advertiseCapabilities);
        await using var fixture = new Fixture(connection: rpc);
        var client = await fixture.StartAsync();
        var initialized = await client.InitializeAsync(new("contract", "Contract", "1"));
        var runtime = new CodexRuntime(fixture.Threads, null, null);
        var info = await runtime.GetInfoAsync();
        Assert.Same(initialized, info.Initialize);
        Assert.NotNull(info.Account);
        Assert.True(info.Account.RequiresOpenaiAuth);
        Assert.Equal("retained", info.Account.Raw.GetProperty("future").GetString());
        if (advertiseCapabilities)
            Assert.True(info.Capabilities!.Value.GetProperty("futureFeature").GetBoolean());
        else Assert.Null(info.Capabilities);
        Assert.Equal(new[] { "initialize", "account/read" }, rpc.Methods);
        Assert.False(rpc.AccountParams.GetProperty("refreshToken").GetBoolean());
        Assert.Single(info.Diagnostics);
        Assert.Contains("unavailable", info.Diagnostics[0]);
        Assert.Null(info.ActualVersion);
        Assert.Null(info.IsVersionMatch);

        await runtime.GetInfoAsync();
        Assert.Equal(2, fixture.Starts); // One direct initialization and one shared connection acquisition.
        Assert.Equal(2, rpc.Methods.Count(method => method == "account/read"));
    }

    [Fact]
    public async Task SkippingServerProbe_DoesNotAcquireConnectionOrReadAccount()
    {
        var rpc = new ProbeRpc(true);
        await using var fixture = new Fixture(connection: rpc);
        var info = await new CodexRuntime(fixture.Threads, null, null).GetInfoAsync(includeServer: false);
        Assert.Equal(0, fixture.Starts);
        Assert.Empty(rpc.Methods);
        Assert.Null(info.Initialize);
        Assert.Null(info.Account);
        Assert.Null(info.Capabilities);
    }

    [Fact]
    public async Task AccountFailure_PreservesAvailableMetadataAndReportsDiagnostic()
    {
        var rpc = new ProbeRpc(true) { AccountFailure = new IOException("account unavailable") };
        await using var fixture = new Fixture(connection: rpc);
        var client = await fixture.StartAsync();
        var initialized = await client.InitializeAsync(new("contract", "Contract", "1"));
        var info = await new CodexRuntime(fixture.Threads, null, null).GetInfoAsync();
        Assert.Same(initialized, info.Initialize);
        Assert.True(info.Capabilities!.Value.GetProperty("futureFeature").GetBoolean());
        Assert.Null(info.Account);
        Assert.Contains(info.Diagnostics, diagnostic => diagnostic.Contains("Server preflight failed: IOException: account unavailable"));
    }

    [Fact]
    public async Task CancellationWhileReadingAccount_PropagatesAndLeavesSharedConnectionUsable()
    {
        var rpc = new ProbeRpc(true) { WaitForCancellation = true };
        await using var fixture = new Fixture(connection: rpc);
        var client = await fixture.StartAsync();
        await client.InitializeAsync(new("contract", "Contract", "1"));
        var runtime = new CodexRuntime(fixture.Threads, null, null);
        using var cancellation = new CancellationTokenSource();
        var probe = runtime.GetInfoAsync(ct: cancellation.Token);
        await rpc.AccountRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.WaitAsync(TimeSpan.FromSeconds(5)));
        rpc.WaitForCancellation = false;
        var retried = await runtime.GetInfoAsync();
        Assert.NotNull(retried.Account);
        Assert.Equal(2, fixture.Starts);
    }

    private sealed class ProbeRpc(bool advertiseCapabilities) : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification { add { } remove { } }
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public List<string> Methods { get; } = [];
        public JsonElement AccountParams { get; private set; }
        public Exception? AccountFailure { get; init; }
        public bool WaitForCancellation { get; set; }
        public TaskCompletionSource AccountRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct)
        {
            Methods.Add(method);
            if (method == "initialize") return JsonSerializer.Deserialize<JsonElement>(advertiseCapabilities
                ? """{"serverName":"contract","capabilities":{"futureFeature":true}}""" : "{}");
            Assert.Equal("account/read", method);
            AccountParams = JsonSerializer.SerializeToElement(parameters);
            AccountRequested.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            if (AccountFailure is not null) throw AccountFailure;
            return JsonSerializer.Deserialize<JsonElement>("""{"account":null,"requiresOpenaiAuth":true,"future":"retained"}""");
        }

        public Task SendNotificationAsync(string method, object? parameters, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
