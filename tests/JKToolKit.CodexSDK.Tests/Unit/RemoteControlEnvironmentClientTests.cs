using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteControlEnvironmentClientTests
{
    [Fact]
    public async Task RemoteControlMethods_WhenExperimentalDisabled_ThrowBeforeSendingRequest()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement };
        await using var client = CreateClient(rpc, experimentalApi: false);

        var act = async () => await client.ReadRemoteControlStatusAsync();

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);

        var pairingAct = async () => await client.StartRemoteControlPairingAsync(new RemoteControlPairingStartOptions());

        await pairingAct.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoteControlMethods_ParseStatusEnvelope()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "status": "connected",
              "serverName": "codex-remote",
              "installationId": "install-1",
              "environmentId": "env-1"
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.EnableRemoteControlAsync();

        result.Status.Should().Be("connected");
        result.StatusValue.Should().Be(RemoteControlConnectionStatus.Connected);
        result.ServerName.Should().Be("codex-remote");
        result.InstallationId.Should().Be("install-1");
        result.EnvironmentId.Should().Be("env-1");
        rpc.LastMethod.Should().Be("remoteControl/enable");

        await client.DisableRemoteControlAsync();
        rpc.LastMethod.Should().Be("remoteControl/disable");

        await client.ReadRemoteControlStatusAsync();
        rpc.LastMethod.Should().Be("remoteControl/status/read");
    }

    [Fact]
    public async Task StartRemoteControlPairingAsync_WhenExperimentalEnabled_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "pairingCode": "pair-1",
              "manualPairingCode": "123-456",
              "environmentId": "env-1",
              "expiresAt": 1770000000
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.StartRemoteControlPairingAsync(new RemoteControlPairingStartOptions
        {
            ManualCode = true
        });

        rpc.LastMethod.Should().Be("remoteControl/pairing/start");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"manualCode\":true");
        result.PairingCode.Should().Be("pair-1");
        result.ManualPairingCode.Should().Be("123-456");
        result.EnvironmentId.Should().Be("env-1");
        result.ExpiresAt.Should().Be(1770000000);
    }

    [Fact]
    public async Task ListRemoteControlClientsAsync_WhenExperimentalEnabled_SendsExpectedParams_AndParsesResult()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "data": [
                {
                  "clientId": "client-1",
                  "displayName": "Phone",
                  "deviceType": "phone",
                  "platform": "ios",
                  "osVersion": "18.0",
                  "deviceModel": "iPhone",
                  "appVersion": "1.2.3",
                  "lastSeenAt": 1770000001
                }
              ],
              "nextCursor": "next-1"
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.ListRemoteControlClientsAsync(new RemoteControlClientsListOptions
        {
            EnvironmentId = "env-1",
            Cursor = "cursor-1",
            Limit = 25,
            Order = RemoteControlClientsListOrder.Desc
        });

        rpc.LastMethod.Should().Be("remoteControl/client/list");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"environmentId\":\"env-1\"")
            .And.Contain("\"cursor\":\"cursor-1\"")
            .And.Contain("\"limit\":25")
            .And.Contain("\"order\":\"desc\"");
        result.Clients.Should().ContainSingle();
        result.Clients[0].ClientId.Should().Be("client-1");
        result.Clients[0].DisplayName.Should().Be("Phone");
        result.Clients[0].LastSeenAt.Should().Be(1770000001);
        result.NextCursor.Should().Be("next-1");
    }

    [Fact]
    public async Task RevokeRemoteControlClientAsync_WhenExperimentalEnabled_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        var result = await client.RevokeRemoteControlClientAsync(new RemoteControlClientsRevokeOptions
        {
            EnvironmentId = "env-1",
            ClientId = "client-1"
        });

        rpc.LastMethod.Should().Be("remoteControl/client/revoke");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"environmentId\":\"env-1\"")
            .And.Contain("\"clientId\":\"client-1\"");
        result.Raw.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task AddEnvironment_WhenExperimentalEnabled_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc, experimentalApi: true);

        await client.AddEnvironmentAsync(new EnvironmentAddOptions
        {
            EnvironmentId = "env-1",
            ExecServerUrl = "https://exec.example.test"
        });

        rpc.LastMethod.Should().Be("environment/add");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"environmentId\":\"env-1\"")
            .And.Contain("\"execServerUrl\":\"https://exec.example.test\"");
    }

    [Fact]
    public async Task AddEnvironment_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement };
        await using var client = CreateClient(rpc, experimentalApi: false);

        var act = async () => await client.AddEnvironmentAsync(new EnvironmentAddOptions
        {
            EnvironmentId = "env-1",
            ExecServerUrl = "https://exec.example.test"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    private static CodexAppServerClient CreateClient(RecordingRpc rpc, bool experimentalApi) =>
        new(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi },
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

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
