using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class RemoteAppServerManagerTests
{
    [Fact]
    public async Task Attachment_DisposesTunnelEvenWhenClientDisposalFails()
    {
        var transport = new FakeProcess();
        var error = new IOException("client disposal failed");
        var attachment = new CodexRemoteAppServerAttachment(SshEntry("ssh"), new Uri("ws://localhost"), CreateClient(new FakeLifetime { DisposeError = error }), transport);
        (await Assert.ThrowsAsync<IOException>(() => attachment.DisposeAsync().AsTask())).Should().BeSameAs(error);
        transport.DisposeCount.Should().Be(1);
        await attachment.DisposeAsync();
        transport.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task SshProbe_DoesNotConvertCallerCancellationIntoNotReady()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe { Probe = ct => { cancellation.Cancel(); return Task.FromCanceled<bool>(ct); } };
        var context = new RemoteAppServerManagerContext(new(), runner, health, (_, _) => Task.FromResult(CreateClient()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RemoteAppServerConnector(context).ProbeAsync(SshEntry("ssh"), cancellation.Token));
        runner.StartedProcesses.Single().DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task RefreshDirect_ReadyEndpointPersistsRunningStatus()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker") with { Status = CodexRemoteAppServerStatus.Stale });
        var health = new RecordingHealthProbe { IsReady = true };
        var entries = await CreateManager(new RecordingProcessRunner(), health, registry).ListAsync(refresh: true);
        entries.Single().Status.Should().Be(CodexRemoteAppServerStatus.Running);
        health.ProbedUris.Should().ContainSingle().Which.Should().Be(new Uri("ws://127.0.0.1:4500"));
        (await registry.GetAsync("docker"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
    }

    [Fact]
    public async Task AttachSsh_CancellationDuringRetryDelayStopsFurtherAttempts()
    {
        using var cancellation = new CancellationTokenSource();
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe { Probe = _ => { cancellation.Cancel(); return Task.FromResult(false); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateManager(runner, health, registry).AttachAsync("ssh", ct: cancellation.Token));
        runner.StartedProcesses.Should().ContainSingle().Which.DisposeCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachSsh_FailureAfterStartingTunnel_DisposesTransport(bool failProbe)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var failure = new IOException("connection failed");
        var health = new RecordingHealthProbe { IsReady = true, Probe = failProbe ? _ => Task.FromException<bool>(failure) : null };
        var manager = new CodexRemoteAppServerManager(new() { Registry = registry }, runner, health, (_, _) => Task.FromException<CodexAppServerClient>(failure));

        var error = await Assert.ThrowsAsync<IOException>(() => manager.AttachAsync("ssh"));

        error.Should().BeSameAs(failure);
        runner.StartedProcesses.Should().ContainSingle().Which.DisposeCount.Should().Be(1);
        (await registry.GetAsync("ssh"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
    }

    [Fact]
    public async Task AttachSsh_CancelDuringReadiness_DisposesTransport()
    {
        using var cancellation = new CancellationTokenSource();
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe { Probe = ct => { cancellation.Cancel(); return Task.FromCanceled<bool>(ct); } };
        var manager = CreateManager(runner, health, registry);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.AttachAsync("ssh", ct: cancellation.Token));

        runner.StartedProcesses.Should().ContainSingle().Which.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task AttachSsh_ExhaustsRetriesAndDisposesEveryTunnel()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var manager = CreateManager(runner, new RecordingHealthProbe(), registry);

        await Assert.ThrowsAsync<TimeoutException>(() => manager.AttachAsync("ssh"));

        runner.StartedProcesses.Should().HaveCount(5).And.OnlyContain(x => x.DisposeCount == 1);
    }

    [Fact]
    public async Task AttachDirect_ClonesOptionsAndAppliesOverrideAndToken()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker") with { BearerToken = "stored" });
        var defaults = new CodexAppServerClientOptions { StartupTimeout = TimeSpan.FromSeconds(99) };
        CodexAppServerWebSocketOptions? received = null;
        var manager = new CodexRemoteAppServerManager(new() { Registry = registry, ClientOptions = defaults, AttachTimeout = TimeSpan.FromSeconds(8) }, new RecordingProcessRunner(), new RecordingHealthProbe(), (options, _) => { received = options; return Task.FromResult(CreateClient()); });
        var configured = false;
        var attachment = await manager.AttachAsync("docker", new() { BearerToken = "override", ConfigureClientOptions = options => { configured = true; options.StartupTimeout = TimeSpan.FromSeconds(1); } });

        configured.Should().BeTrue();
        received!.BearerToken.Should().Be("override");
        received.Uri.Should().Be(attachment.EndpointUri);
        received.ClientOptions.Should().NotBeSameAs(defaults);
        received.ClientOptions.StartupTimeout.Should().Be(TimeSpan.FromSeconds(8));
        defaults.StartupTimeout.Should().Be(TimeSpan.FromSeconds(99));
        attachment.Entry.Id.Should().Be("docker");
        await attachment.DisposeAsync();
        await attachment.DisposeAsync();
        (await registry.GetAsync("docker"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
    }

    [Fact]
    public async Task AttachDirect_UsesStoredTokenAndPerAttachmentOptions()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker") with { BearerToken = "stored" });
        var supplied = new CodexAppServerClientOptions { NotificationBufferCapacity = 37 };
        CodexAppServerWebSocketOptions? received = null;
        var manager = new CodexRemoteAppServerManager(new() { Registry = registry }, new RecordingProcessRunner(), new RecordingHealthProbe(), (options, _) => { received = options; return Task.FromResult(CreateClient()); });
        await using var attachment = await manager.AttachAsync("docker", new() { ClientOptions = supplied });
        received!.BearerToken.Should().Be("stored");
        received.ClientOptions.Should().NotBeSameAs(supplied);
        received.ClientOptions.NotificationBufferCapacity.Should().Be(37);
    }

    [Theory]
    [InlineData(CodexRemoteAppServerKind.SshWebSocket)]
    [InlineData(CodexRemoteAppServerKind.DockerContainerWebSocket)]
    public async Task Attach_RejectsIncompleteRegistryEntry(CodexRemoteAppServerKind kind)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(new() { Id = "broken", Kind = kind });
        var runner = new RecordingProcessRunner();
        var manager = CreateManager(runner, new RecordingHealthProbe(), registry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.AttachAsync("broken"));
        runner.StartLaunches.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingEntryAndInvalidId_DoNotLaunchCommands()
    {
        var runner = new RecordingProcessRunner();
        var manager = CreateManager(runner, new RecordingHealthProbe());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.AttachAsync("missing"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.StopAsync("missing"));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.AttachAsync(" "));
        (await manager.RemoveAsync("missing")).Should().BeFalse();
        runner.RunLaunches.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task StopDocker_UsesConfiguredRemovalPolicyAndRegistryPolicy(bool removeContainer, bool removeEntry)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        var entry = DockerEntry("docker");
        await registry.UpsertAsync(entry with { Docker = entry.Docker! with { RemoveContainerOnStop = removeContainer } });
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        var manager = CreateManager(runner, new RecordingHealthProbe(), registry);

        var stopped = await manager.StopAsync("docker", new() { RemoveFromRegistry = removeEntry });

        stopped.Status.Should().Be(CodexRemoteAppServerStatus.Stopped);
        stopped.UpdatedAt.Should().BeOnOrAfter(entry.UpdatedAt);
        runner.RunLaunches.Single().Arguments.Should().Equal(removeContainer ? ["rm", "-f", "codex-dev"] : new[] { "stop", "codex-dev" });
        if (removeEntry) (await registry.GetAsync("docker")).Should().BeNull();
        else (await registry.GetAsync("docker"))!.Status.Should().Be(CodexRemoteAppServerStatus.Stopped);
    }

    [Fact]
    public async Task StopSsh_UsesOverridePasswordAndQuotedPidPath()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        var entry = SshEntry("ssh");
        await registry.UpsertAsync(entry with { Ssh = entry.Ssh! with { RemotePidFile = "/state's/pid" } });
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        var manager = CreateManager(runner, new RecordingHealthProbe(), registry);
        var stopped = await manager.StopAsync("ssh", new() { SshPassword = "secret" });
        stopped.Status.Should().Be(CodexRemoteAppServerStatus.Stopped);
        runner.RunLaunches.Single().Environment.Should().Contain("SSHPASS", "secret");
        runner.RunLaunches.Single().Arguments.Last().Should().Contain("'/state'\"'\"'s/pid'");
    }

    [Fact]
    public async Task StopDockerExec_KillsPidWithoutRemovingContainer()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        var entry = DockerEntry("exec");
        await registry.UpsertAsync(entry with { Kind = CodexRemoteAppServerKind.DockerExecWebSocket, Docker = entry.Docker! with { PidFile = "/state/pid" } });
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        await CreateManager(runner, new RecordingHealthProbe(), registry).StopAsync("exec");
        runner.RunLaunches.Single().Arguments.Should().StartWith("exec", "codex-dev", "sh", "-lc");
        runner.RunLaunches.Single().Arguments.Last().Should().Contain("kill").And.Contain("'/state/pid'");
    }

    [Fact]
    public async Task FailedStop_PreservesRunningEntry()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker"));
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("", 3, "cannot stop");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateManager(runner, new RecordingHealthProbe(), registry).StopAsync("docker", new() { RemoveFromRegistry = true }));
        error.Message.Should().Contain("3").And.Contain("cannot stop");
        (await registry.GetAsync("docker"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
    }

    [Theory]
    [InlineData(CodexRemoteAppServerKind.SshWebSocket)]
    [InlineData(CodexRemoteAppServerKind.DockerContainerWebSocket)]
    [InlineData(CodexRemoteAppServerKind.DockerExecWebSocket)]
    [InlineData((CodexRemoteAppServerKind)99)]
    public async Task Stop_RejectsInvalidRegistryDetails(CodexRemoteAppServerKind kind)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(new() { Id = "broken", Kind = kind });
        var manager = CreateManager(new RecordingProcessRunner(), new RecordingHealthProbe(), registry);
        if ((int)kind == 99) await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.StopAsync("broken"));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StopAsync("broken"));
    }

    [Fact]
    public async Task ListWithoutRefresh_DoesNotProbeAndRemoveDoesNotStop()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker"));
        var runner = new RecordingProcessRunner();
        var health = new RecordingHealthProbe();
        var manager = CreateManager(runner, health, registry);
        (await manager.ListAsync()).Should().ContainSingle();
        health.ProbedUris.Should().BeEmpty();
        (await manager.RemoveAsync("docker")).Should().BeTrue();
        (await manager.ListAsync()).Should().BeEmpty();
        runner.RunLaunches.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshSsh_DisposesTunnelAndPersistsHealth(bool ready)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var result = await CreateManager(runner, new RecordingHealthProbe { IsReady = ready }, registry).ListAsync(refresh: true);
        result.Single().Status.Should().Be(ready ? CodexRemoteAppServerStatus.Running : CodexRemoteAppServerStatus.Stale);
        runner.StartedProcesses.Single().DisposeCount.Should().Be(1);
        (await registry.GetAsync("ssh"))!.Status.Should().Be(result.Single().Status);
    }

    [Fact]
    public async Task RefreshSsh_ProbeExceptionMarksStale()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var result = await CreateManager(runner, new RecordingHealthProbe { Probe = _ => Task.FromException<bool>(new IOException()) }, registry).ListAsync(refresh: true);
        result.Single().Status.Should().Be(CodexRemoteAppServerStatus.Stale);
        runner.StartedProcesses.Single().DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task RefreshSsh_CallerCancellationPropagatesAndPreservesEntry()
    {
        using var cancellation = new CancellationTokenSource();
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(SshEntry("ssh"));
        var runner = new RecordingProcessRunner();
        var manager = CreateManager(runner, new RecordingHealthProbe { Probe = ct => { cancellation.Cancel(); return Task.FromCanceled<bool>(ct); } }, registry);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ListAsync(refresh: true, ct: cancellation.Token));
        (await registry.GetAsync("ssh"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
        runner.StartedProcesses.Single().DisposeCount.Should().Be(1);
    }

    [Theory]
    [InlineData(CodexRemoteAppServerKind.SshWebSocket)]
    [InlineData(CodexRemoteAppServerKind.DockerContainerWebSocket)]
    public async Task RefreshMissingTransportDetails_MarksStale(CodexRemoteAppServerKind kind)
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(new() { Id = "broken", Kind = kind });
        var result = await CreateManager(new RecordingProcessRunner(), new RecordingHealthProbe(), registry).ListAsync(refresh: true);
        result.Single().Status.Should().Be(CodexRemoteAppServerStatus.Stale);
    }
}
