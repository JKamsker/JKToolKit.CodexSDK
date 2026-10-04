using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class RemoteAppServerManagerTests
{
    [Fact]
    public void Manager_RejectsMissingDependencies()
    {
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe();
        Func<CodexAppServerWebSocketOptions, CancellationToken, Task<CodexAppServerClient>> factory = (_, _) => Task.FromResult(CreateClient());
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerManager(null!, runner, health, factory));
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerManager(new(), null!, health, factory));
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerManager(new(), runner, null!, factory));
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerManager(new(), runner, health, null!));
    }

    [Fact]
    public void Orchestration_RejectsMissingDependencies()
    {
        var context = new RemoteAppServerManagerContext(new(), new RecordingProcessRunner(), new RecordingHealthProbe(), (_, _) => Task.FromResult(CreateClient()));
        Assert.Throws<ArgumentNullException>(() => new RemoteAppServerConnector(null!));
        Assert.Throws<ArgumentNullException>(() => new RemoteAppServerStarter(null!));
        Assert.Throws<ArgumentNullException>(() => new RemoteAppServerLifecycle(null!, new RemoteAppServerConnector(context)));
        Assert.Throws<ArgumentNullException>(() => new RemoteAppServerLifecycle(context, null!));
    }

    [Fact]
    public async Task Attachment_RejectsMissingDependencies()
    {
        await using var client = CreateClient();
        var entry = SshEntry("ssh");
        var uri = new Uri("ws://localhost");
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerAttachment(null!, uri, client, null));
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerAttachment(entry, null!, client, null));
        Assert.Throws<ArgumentNullException>(() => new CodexRemoteAppServerAttachment(entry, uri, null!, null));
    }

    [Fact]
    public async Task StopDockerExec_MissingPidFailsWithoutLaunching()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("exec") with { Kind = CodexRemoteAppServerKind.DockerExecWebSocket });
        var runner = new RecordingProcessRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateManager(runner, new RecordingHealthProbe(), registry).StopAsync("exec"));
        runner.RunLaunches.Should().BeEmpty();
    }

    [Fact]
    public async Task AttachSsh_ExplicitPasswordOverridesRememberedPassword()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun(RemoteMetadata("ssh", "1", "ws://localhost:4500", "/state"));
        var manager = CreateManager(runner, new RecordingHealthProbe { IsReady = true });
        await manager.StartSshWebSocketAsync(new() { Id = "ssh", Host = "host", Password = "original" });
        await using var attachment = await manager.AttachAsync("ssh", new() { SshPassword = "override" });
        runner.StartLaunches.Single().Environment.Should().Contain("SSHPASS", "override");
    }

    [Fact]
    public async Task WaitReady_ZeroTimeoutStillChecksReadiness()
    {
        var health = new RecordingHealthProbe { IsReady = true };
        var context = new RemoteAppServerManagerContext(new() { StartTimeout = TimeSpan.Zero }, new RecordingProcessRunner(), health, (_, _) => Task.FromResult(CreateClient()));
        await context.WaitReadyAsync(new Uri("ws://localhost"), default);
        health.ProbedUris.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DockerExec_ZeroTimeoutDoesNotPollMetadata()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        var manager = new CodexRemoteAppServerManager(new() { StartTimeout = TimeSpan.Zero }, runner, new RecordingHealthProbe(), (_, _) => Task.FromResult(CreateClient()));
        await Assert.ThrowsAsync<TimeoutException>(() => manager.StartDockerExecWebSocketAsync(new() { Container = "container", PublicUri = new Uri("ws://localhost") }));
        runner.RunLaunches.Should().ContainSingle();
    }

    [Fact]
    public async Task DockerExec_RetriesIncompleteMetadataBeforeRegistration()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        runner.EnqueueRun("starting");
        runner.EnqueueRun(RemoteMetadata("exec", "1", "ws://localhost:4500", "/state"));
        var manager = new CodexRemoteAppServerManager(new() { StartTimeout = TimeSpan.FromSeconds(10) }, runner, new RecordingHealthProbe { IsReady = true }, (_, _) => Task.FromResult(CreateClient()));
        var entry = await manager.StartDockerExecWebSocketAsync(new() { Id = "exec", Container = "container", PublicUri = new Uri("ws://localhost") });
        runner.RunLaunches.Should().HaveCount(3);
        (await manager.ListAsync()).Single().Should().Be(entry);
    }
}
