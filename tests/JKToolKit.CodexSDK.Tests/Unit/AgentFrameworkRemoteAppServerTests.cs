using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Agents.Remote;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkRemoteAppServerTests
{
    [Fact]
    public async Task StartAppServerAsync_WithRemoteAppServer_AttachesAndConfiguresDynamicToolApproval()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(new CodexRemoteAppServerEntry
        {
            Id = "remote-1",
            Kind = CodexRemoteAppServerKind.DockerContainerWebSocket,
            Status = CodexRemoteAppServerStatus.Running,
            WebSocketUri = new Uri("ws://127.0.0.1:4500"),
            Docker = new CodexRemoteDockerAppServerInfo { ContainerName = "codex-dev" }
        });
        var captured = new List<CodexAppServerWebSocketOptions>();
        var manager = new CodexRemoteAppServerManager(
            new CodexRemoteAppServerManagerOptions { Registry = registry },
            new ThrowingProcessRunner(),
            new ReadyHealthProbe(),
            (options, _) =>
            {
                captured.Add(options);
                return Task.FromResult(CreateClient());
            });
        var approvalHandler = new FakeApprovalHandler();
        var agentOptions = new CodexAIAgentOptions
        {
            RemoteAppServer = new CodexAgentRemoteAppServerOptions
            {
                Manager = manager,
                EntryId = "remote-1",
                AttachOptions = new CodexRemoteAttachOptions
                {
                    ConfigureClientOptions = options => options.NotificationBufferCapacity = 7
                }
            }
        };

        await using var lease = await new CodexAgentClient()
            .StartAppServerAsync(approvalHandler, agentOptions, CancellationToken.None);

        lease.Client.Should().NotBeNull();
        var webSocketOptions = captured.Should().ContainSingle().Subject;
        webSocketOptions.Uri.Should().Be(new Uri("ws://127.0.0.1:4500"));
        webSocketOptions.ClientOptions.NotificationBufferCapacity.Should().Be(7);
        webSocketOptions.ClientOptions.ExperimentalApi.Should().BeTrue();
        webSocketOptions.ClientOptions.ApprovalHandler.Should().BeSameAs(approvalHandler);
    }

    private static CodexAppServerClient CreateClient() =>
        new(
            new CodexAppServerClientOptions(),
            new FakeLifetime(),
            new FakeRpc(),
            NullLogger.Instance,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            startExitWatcher: false);

    private sealed class ThrowingProcessRunner : IRemoteProcessRunner
    {
        public Task<RemoteProcessResult> RunAsync(CodexLaunch launch, TimeSpan timeout, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IAsyncDisposableProcess> StartAsync(CodexLaunch launch, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class ReadyHealthProbe : IRemoteAppServerHealthProbe
    {
        public Task<bool> IsReadyAsync(Uri webSocketUri, TimeSpan timeout, CancellationToken ct) =>
            Task.FromResult(true);
    }

    private sealed class FakeApprovalHandler : IAppServerApprovalHandler
    {
        public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct) =>
            new(JsonDocument.Parse("{}").RootElement.Clone());
    }

    private sealed class FakeLifetime : IAppServerLifetime
    {
        public Task Completion { get; } = Task.CompletedTask;

        public int? ProcessId => null;

        public int? ExitCode => null;

        public IReadOnlyList<string> DiagnosticTail => [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            Task.FromResult(JsonDocument.Parse("{}").RootElement.Clone());

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Touch() => OnNotification?.Invoke(new JsonRpcNotification("noop", null));
    }
}
