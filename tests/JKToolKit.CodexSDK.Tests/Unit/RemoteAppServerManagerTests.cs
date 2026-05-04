using System.Text.Json;
using FluentAssertions;
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

public sealed class RemoteAppServerManagerTests
{
    [Fact]
    public async Task StartSshWebSocketAsync_UsesLoopbackPortZero_AndQuotesRemotePaths()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun(RemoteMetadata("remote-1", "123", "ws://127.0.0.1:46123", "/state dir"));
        var manager = CreateManager(runner, new RecordingHealthProbe());

        var entry = await manager.StartSshWebSocketAsync(new CodexSshWebSocketAppServerOptions
        {
            Id = "remote-1",
            Host = "devbox",
            Password = "ssh-secret",
            RemoteWorkingDirectory = "/workspace/project's repo",
            RemoteStateDirectory = "/state dir"
        });

        entry.Ssh!.RemotePort.Should().Be(46123);
        var launch = runner.RunLaunches.Should().ContainSingle().Subject;
        launch.FileName.Should().Be("sshpass");
        launch.Environment.Should().Contain("SSHPASS", "ssh-secret");
        launch.Arguments.Should().Contain("ssh");
        var script = launch.Arguments.Last();
        script.Should().Contain("ws://127.0.0.1:0");
        script.Should().Contain("cd '/workspace/project'\"'\"'s repo'");
        script.Should().Contain("state_dir='/state dir'");
        launch.Arguments.Should().NotContain("ssh-secret");
    }

    [Fact]
    public async Task StartDockerContainerWebSocketAsync_UsesLabelsLoopbackPublish_AndFixedContainerPort()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id");
        runner.EnqueueRun("127.0.0.1:49153");
        var health = new RecordingHealthProbe { IsReady = true };
        var manager = CreateManager(runner, health);

        var entry = await manager.StartDockerContainerWebSocketAsync(new CodexDockerContainerWebSocketAppServerOptions
        {
            Id = "remote-1",
            Image = "codex-image",
            CodexHome = "/home/codex/.codex",
            WorkingDirectory = "/workspace"
        });

        entry.WebSocketUri.Should().Be(new Uri("ws://127.0.0.1:49153"));
        var run = runner.RunLaunches[0];
        run.Arguments.Should().ContainInOrder("run", "-d", "--label", "jktoolkit.codexsdk.remote=true");
        run.Arguments.Should().ContainInOrder("--label", "jktoolkit.codexsdk.id=remote-1");
        run.Arguments.Should().ContainInOrder("-p", "127.0.0.1::4500");
        run.Arguments.Should().ContainInOrder("codex-image", "codex", "app-server", "--listen", "ws://0.0.0.0:4500");
    }

    [Fact]
    public async Task StartDockerExecWebSocketAsync_RequiresPublicWebSocketUri_AndWritesPidLogPaths()
    {
        var badOptions = new CodexDockerExecWebSocketAppServerOptions
        {
            Container = "codex-dev",
            PublicUri = new Uri("http://127.0.0.1:4500")
        };
        var manager = CreateManager(new RecordingProcessRunner(), new RecordingHealthProbe());

        var act = () => manager.StartDockerExecWebSocketAsync(badOptions);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ws:// or wss://*");

        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        runner.EnqueueRun(RemoteMetadata("remote-1", "44", "ws://0.0.0.0:4500", "/codex-state"));
        manager = CreateManager(runner, new RecordingHealthProbe { IsReady = true });

        var entry = await manager.StartDockerExecWebSocketAsync(new CodexDockerExecWebSocketAppServerOptions
        {
            Id = "remote-1",
            Container = "codex-dev",
            PublicUri = new Uri("ws://127.0.0.1:55000"),
            StateDirectory = "/codex-state"
        });

        entry.Docker!.PidFile.Should().Be("/codex-state/remote-1.pid");
        entry.Docker.LogFile.Should().Be("/codex-state/remote-1.log");
        var start = runner.RunLaunches[0];
        start.Arguments.Should().ContainInOrder("exec", "-d", "codex-dev", "sh", "-lc");
        start.Arguments.Last().Should().Contain("printf '%s\\n' \"$$\" > \"$pid_file\"");
        start.Arguments.Last().Should().Contain("exec 'codex' 'app-server' '--listen' 'ws://0.0.0.0:4500'");
    }

    [Fact]
    public async Task AttachAsync_ForSsh_StartsTunnel_AndAttachmentDisposeDoesNotStopEntry()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("remote-1"));
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe { IsReady = true };
        var manager = CreateManager(runner, health, registry);

        await using (var attachment = await manager.AttachAsync("remote-1"))
        {
            attachment.EndpointUri.Host.Should().Be("127.0.0.1");
        }

        runner.StartLaunches.Should().ContainSingle();
        runner.StartLaunches[0].Arguments.Should().Contain("-N");
        runner.StartLaunches[0].Arguments.Should().Contain(arg => arg.Contains(":127.0.0.1:45123", StringComparison.Ordinal));
        runner.StartedProcesses.Should().ContainSingle().Which.Disposed.Should().BeTrue();
        (await registry.GetAsync("remote-1"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
    }

    [Fact]
    public async Task ListAsync_WithRefresh_MarksStaleWithoutDeletingEntry()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("remote-1"));
        var manager = CreateManager(new RecordingProcessRunner(), new RecordingHealthProbe { IsReady = false }, registry);

        var entries = await manager.ListAsync(refresh: true);

        entries.Should().ContainSingle().Which.Status.Should().Be(CodexRemoteAppServerStatus.Stale);
        (await registry.GetAsync("remote-1")).Should().NotBeNull();
    }

    private static CodexRemoteAppServerManager CreateManager(
        RecordingProcessRunner runner,
        RecordingHealthProbe health,
        ICodexRemoteAppServerRegistry? registry = null) =>
        new(
            new CodexRemoteAppServerManagerOptions
            {
                Registry = registry ?? new InMemoryCodexRemoteAppServerRegistry(),
                HealthCheckTimeout = TimeSpan.FromMilliseconds(10),
                StartTimeout = TimeSpan.FromMilliseconds(200)
            },
            runner,
            health,
            (options, _) => Task.FromResult(CreateClient()));

    private static CodexAppServerClient CreateClient() =>
        new(
            new CodexAppServerClientOptions(),
            new FakeLifetime(),
            new FakeRpc(),
            NullLogger.Instance,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            startExitWatcher: false);

    private static string RemoteMetadata(string id, string pid, string uri, string stateDir) =>
        $"""
        CODEXSDK_ID={id}
        CODEXSDK_PID={pid}
        CODEXSDK_URI={uri}
        CODEXSDK_STATE_DIR={stateDir}
        CODEXSDK_PID_FILE={stateDir}/{id}.pid
        CODEXSDK_LOG_FILE={stateDir}/{id}.log
        """;

    private static CodexRemoteAppServerEntry SshEntry(string id) => new()
    {
        Id = id,
        Kind = CodexRemoteAppServerKind.SshWebSocket,
        Status = CodexRemoteAppServerStatus.Running,
        Ssh = new CodexRemoteSshAppServerInfo
        {
            Host = "devbox",
            RemoteStateDirectory = "/state",
            RemotePidFile = "/state/remote-1.pid",
            RemoteLogFile = "/state/remote-1.log",
            RemotePort = 45123
        }
    };

    private static CodexRemoteAppServerEntry DockerEntry(string id) => new()
    {
        Id = id,
        Kind = CodexRemoteAppServerKind.DockerContainerWebSocket,
        Status = CodexRemoteAppServerStatus.Running,
        WebSocketUri = new Uri("ws://127.0.0.1:4500"),
        Docker = new CodexRemoteDockerAppServerInfo { ContainerName = "codex-dev" }
    };

    private sealed class RecordingProcessRunner : IRemoteProcessRunner
    {
        private readonly Queue<RemoteProcessResult> _runResults = new();

        public List<CodexLaunch> RunLaunches { get; } = [];

        public List<CodexLaunch> StartLaunches { get; } = [];

        public List<FakeProcess> StartedProcesses { get; } = [];

        public void EnqueueRun(string stdout, int exitCode = 0, string stderr = "") =>
            _runResults.Enqueue(new RemoteProcessResult(exitCode, stdout, stderr));

        public Task<RemoteProcessResult> RunAsync(CodexLaunch launch, TimeSpan timeout, CancellationToken ct)
        {
            RunLaunches.Add(launch);
            if (_runResults.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No enqueued remote process result for launch '{launch.FileName} {string.Join(" ", launch.Arguments)}'. " +
                    $"Recorded launches: {RunLaunches.Count}.");
            }

            return Task.FromResult(_runResults.Dequeue());
        }

        public Task<IAsyncDisposableProcess> StartAsync(CodexLaunch launch, CancellationToken ct)
        {
            StartLaunches.Add(launch);
            var process = new FakeProcess();
            StartedProcesses.Add(process);
            return Task.FromResult<IAsyncDisposableProcess>(process);
        }
    }

    private sealed class RecordingHealthProbe : IRemoteAppServerHealthProbe
    {
        public bool IsReady { get; set; }

        public List<Uri> ProbedUris { get; } = [];

        public Task<bool> IsReadyAsync(Uri webSocketUri, TimeSpan timeout, CancellationToken ct)
        {
            ProbedUris.Add(webSocketUri);
            return Task.FromResult(IsReady);
        }
    }

    private sealed class FakeProcess : IAsyncDisposableProcess
    {
        public bool Disposed { get; private set; }

        public Task Completion { get; } = Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
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

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Touch() => OnNotification?.Invoke(new JsonRpcNotification("noop", null));
    }
}
