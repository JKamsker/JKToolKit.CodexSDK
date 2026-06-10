using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteControlPairingStatusClientTests
{
    [Fact]
    public async Task ReadRemoteControlPairingStatusAsync_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{"claimed": true}""").RootElement };
        await using var client = CreateClient(rpc, experimentalApi: false);

        var act = async () => await client.ReadRemoteControlPairingStatusAsync(new RemoteControlPairingStatusOptions
        {
            PairingCode = "pair-1"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ReadRemoteControlPairingStatusAsync_WhenExperimentalEnabled_SendsPairingCode_AndParsesResult()
    {
        using var doc = JsonDocument.Parse("""{"claimed": true}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.ReadRemoteControlPairingStatusAsync(new RemoteControlPairingStatusOptions
        {
            PairingCode = "pair-1"
        });

        rpc.LastMethod.Should().Be("remoteControl/pairing/status");
        var json = SerializeParams(rpc.LastParams);
        json.Should().Contain("\"pairingCode\":\"pair-1\"");
        json.Should().NotContain("manualPairingCode");
        result.Claimed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadRemoteControlPairingStatusAsync_WhenManualCodeProvided_SendsManualPairingCode()
    {
        using var doc = JsonDocument.Parse("""{"claimed": false}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.ReadRemoteControlPairingStatusAsync(new RemoteControlPairingStatusOptions
        {
            ManualPairingCode = "123-456"
        });

        rpc.LastMethod.Should().Be("remoteControl/pairing/status");
        var json = SerializeParams(rpc.LastParams);
        json.Should().Contain("\"manualPairingCode\":\"123-456\"");
        json.Should().NotContain("pairingCode");
        result.Claimed.Should().BeFalse();
    }

    [Fact]
    public async Task ReadRemoteControlPairingStatusAsync_RequiresExactlyOneCode()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{"claimed": true}""").RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var missing = async () => await client.ReadRemoteControlPairingStatusAsync(new RemoteControlPairingStatusOptions());
        var both = async () => await client.ReadRemoteControlPairingStatusAsync(new RemoteControlPairingStatusOptions
        {
            PairingCode = "pair-1",
            ManualPairingCode = "123-456"
        });

        await missing.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one*PairingCode*ManualPairingCode*");
        await both.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one*PairingCode*ManualPairingCode*");
        rpc.RequestCount.Should().Be(0);
    }

    private static CodexAppServerClient CreateClient(RecordingRpc rpc, bool experimentalApi) =>
        new(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private static string SerializeParams(object? value) =>
        JsonSerializer.Serialize(value, CodexAppServerClient.CreateDefaultSerializerOptions());

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
        public int RequestCount { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
