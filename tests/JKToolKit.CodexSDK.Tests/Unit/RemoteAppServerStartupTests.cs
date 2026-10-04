using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class RemoteAppServerManagerTests
{
    [Fact]
    public async Task StartContainer_CancellationStillCleansUpOwnedContainer()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id");
        runner.EnqueueRun("127.0.0.1:4567");
        runner.EnqueueRun("");
        var health = new RecordingHealthProbe { Probe = ct => { cancellation.Cancel(); return Task.FromCanceled<bool>(ct); } };
        var manager = CreateManager(runner, health);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartDockerContainerWebSocketAsync(new() { Id = "container", Image = "image" }, cancellation.Token));
        runner.RunLaunches.Last().Arguments.Should().Equal("rm", "-f", "container");
        runner.RunTokens.Last().IsCancellationRequested.Should().BeFalse();
        (await manager.ListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task Start_RejectsInvalidPortsBeforeLaunching(int port)
    {
        var runner = new RecordingProcessRunner();
        var manager = CreateManager(runner, new RecordingHealthProbe());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.StartSshWebSocketAsync(new() { Host = "host", Port = port }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.StartDockerContainerWebSocketAsync(new() { Image = "image", ContainerPort = port }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.StartDockerExecWebSocketAsync(new() { Container = "container", PublicUri = new Uri("wss://example.test"), ContainerPort = port }));
        runner.RunLaunches.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_RejectsMissingRequiredSettings()
    {
        var manager = CreateManager(new RecordingProcessRunner(), new RecordingHealthProbe());
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.StartSshWebSocketAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.StartDockerContainerWebSocketAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.StartDockerExecWebSocketAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.StartSshWebSocketAsync(new() { Host = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.StartSshWebSocketAsync(new() { Host = "host", Password = "secret", SshpassExecutable = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.StartDockerContainerWebSocketAsync(new() { Image = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.StartDockerExecWebSocketAsync(new() { Container = " ", PublicUri = new Uri("ws://localhost") }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public async Task StartSsh_PreservesSettingsAndRememberedSecrets(int port)
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun(RemoteMetadata("ssh", "invalid", "ws://127.0.0.1:4500", "/state"));
        runner.EnqueueRun("");
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        CodexAppServerWebSocketOptions? received = null;
        var manager = new CodexRemoteAppServerManager(new() { Registry = registry }, runner, new RecordingHealthProbe { IsReady = true }, (options, _) => { received = options; return Task.FromResult(CreateClient()); });
        var entry = await manager.StartSshWebSocketAsync(new() { Host = "host", Id = "ssh", Name = "name", Port = port, IdentityFile = "/key", ConfigFile = "/config", Username = "user", Password = "secret", BearerToken = "token", AdditionalSshArguments = ["-v"], AdditionalAppServerArguments = ["--some-flag"] });
        entry.Ssh!.RemoteProcessId.Should().BeNull();
        entry.Name.Should().Be("name");
        entry.CreatedAt.Should().Be(entry.UpdatedAt);
        runner.RunLaunches[0].Arguments.Should().ContainInOrder("-F", "/config", "-T", "-i", "/key", "-p", port.ToString(), "-l", "user", "-v", "host");
        runner.RunLaunches[0].Arguments.Last().Should().Contain("${CODEX_HOME:-$HOME/.codex}").And.Contain("'--some-flag'");
        await registry.UpsertAsync(entry with { BearerToken = "outdated persisted token" });
        await using var attachment = await manager.AttachAsync(entry.Id);
        received!.BearerToken.Should().Be("token");
        runner.StartLaunches.Single().Environment.Should().Contain("SSHPASS", "secret");
        await manager.StopAsync(entry.Id);
        runner.RunLaunches[1].Environment.Should().Contain("SSHPASS", "secret");
    }

    [Fact]
    public async Task StartContainer_GeneratesIdAndHonorsCustomContainerAndArguments()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id\n");
        runner.EnqueueRun("127.0.0.1:4567\n");
        var manager = CreateManager(runner, new RecordingHealthProbe { IsReady = true });
        var entry = await manager.StartDockerContainerWebSocketAsync(new() { Image = "image", ContainerName = "custom", AdditionalDockerRunArguments = ["--read-only"], AdditionalAppServerArguments = ["--flag"], RemoveContainerOnStop = false });
        entry.Id.Should().StartWith("codexsdk-");
        entry.Docker!.ContainerName.Should().Be("custom");
        entry.Docker.ContainerId.Should().Be("container-id");
        entry.Docker.RemoveContainerOnStop.Should().BeFalse();
        entry.WebSocketUri.Should().Be(new Uri("ws://127.0.0.1:4567"));
        runner.RunLaunches[0].Arguments.Should().ContainInOrder("--read-only", "image", "codex", "app-server", "--listen", "ws://0.0.0.0:4500", "--flag");
        (await manager.ListAsync()).Single().Should().Be(entry);
    }

    [Theory]
    [InlineData("unparseable")]
    [InlineData("127.0.0.1:999999999999999999999")]
    public async Task StartContainer_BadPublishedPortRemovesContainerAndDoesNotRegister(string output)
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id");
        runner.EnqueueRun(output);
        runner.EnqueueRun("");
        var manager = CreateManager(runner, new RecordingHealthProbe());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartDockerContainerWebSocketAsync(new() { Id = "container", Image = "image" }));
        runner.RunLaunches.Last().Arguments.Should().Equal("rm", "-f", "container");
        (await manager.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StartContainer_RunFailureDoesNotRemoveUnownedContainer()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("", 125, "name exists");
        var manager = CreateManager(runner, new RecordingHealthProbe());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartDockerContainerWebSocketAsync(new() { Id = "container", Image = "image" }));
        runner.RunLaunches.Should().ContainSingle();
        (await manager.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StartContainer_CleanupFailureDoesNotHideOriginalFailure()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id");
        runner.EnqueueRun("", 4, "port lookup failed");
        // No cleanup result: the recording runner throws to simulate a failed cleanup launch.
        var manager = CreateManager(runner, new RecordingHealthProbe());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartDockerContainerWebSocketAsync(new() { Image = "image" }));
        error.Message.Should().Contain("port lookup failed");
        runner.RunLaunches.Should().HaveCount(3);
    }

    [Fact]
    public async Task StartContainer_ReadinessTimeoutRemovesContainer()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("container-id");
        runner.EnqueueRun("127.0.0.1:4567");
        runner.EnqueueRun("");
        var health = new RecordingHealthProbe();
        var manager = CreateManager(runner, health);
        await Assert.ThrowsAsync<TimeoutException>(() => manager.StartDockerContainerWebSocketAsync(new() { Image = "image" }));
        health.ProbedUris.Count.Should().BeGreaterThanOrEqualTo(2);
        runner.RunLaunches.Last().Arguments[0].Should().Be("rm");
    }

    [Fact]
    public async Task StartDockerExec_UsesEnvironmentAndAdditionalArguments()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        runner.EnqueueRun(RemoteMetadata("exec", "55", "ws://0.0.0.0:4500", "/state"));
        var manager = CreateManager(runner, new RecordingHealthProbe { IsReady = true });
        var entry = await manager.StartDockerExecWebSocketAsync(new() { Id = "exec", Container = "container", PublicUri = new Uri("wss://example.test"), WorkingDirectory = "/work's", CodexHome = "/home/codex", AdditionalDockerExecArguments = ["--user", "codex"], AdditionalAppServerArguments = ["--flag"] });
        runner.RunLaunches[0].Arguments.Should().ContainInOrder("exec", "-w", "/work's", "-e", "CODEX_HOME=/home/codex", "--user", "codex", "-d", "container");
        runner.RunLaunches[0].Arguments.Last().Should().Contain("cd '/work'\"'\"'s'").And.Contain("${CODEX_HOME:-/tmp}").And.Contain("'--flag'");
        entry.Docker!.PidFile.Should().Be("/state/exec.pid");
        entry.WebSocketUri.Should().Be(new Uri("wss://example.test"));
    }

    [Theory]
    [InlineData(1, "", "not ready")]
    [InlineData(0, "incomplete", "")]
    public async Task StartDockerExec_MissingMetadataTimesOutWithoutRegistration(int exitCode, string output, string error)
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueRun("");
        runner.DefaultRunResult = new RemoteProcessResult(exitCode, output, error);
        var manager = new CodexRemoteAppServerManager(new() { StartTimeout = TimeSpan.FromMilliseconds(30) }, runner, new RecordingHealthProbe(), (_, _) => Task.FromResult(CreateClient()));
        var failure = await Assert.ThrowsAsync<TimeoutException>(() => manager.StartDockerExecWebSocketAsync(new() { Container = "container", PublicUri = new Uri("ws://localhost") }));
        failure.Message.Should().Contain("metadata");
        if (error.Length > 0) failure.Message.Should().Contain(error);
        (await manager.ListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("CODEXSDK_ID")]
    [InlineData("CODEXSDK_URI")]
    [InlineData("CODEXSDK_STATE_DIR")]
    [InlineData("CODEXSDK_PID_FILE")]
    [InlineData("CODEXSDK_LOG_FILE")]
    public void Metadata_RequiresEachField(string field)
    {
        var output = string.Join('\n', RemoteMetadata("id", "1", "ws://localhost:4500", "/state").Split('\n').Where(line => !line.StartsWith(field + "=", StringComparison.Ordinal)));
        var failure = Assert.Throws<InvalidOperationException>(() => RemoteStartMetadata.Parse(output));
        failure.Message.Should().Contain(field);
        failure = Assert.Throws<InvalidOperationException>(() => RemoteStartMetadata.Parse(output + $"\n{field}= "));
        failure.Message.Should().Contain(field);
    }

    [Fact]
    public void Metadata_IgnoresNoiseAndPreservesEqualsInPaths()
    {
        var output = "noise\n=ignored\r\n" + RemoteMetadata("id", "123", "ws://localhost:4500", "/state=dir");
        var parsed = RemoteStartMetadata.Parse(output);
        parsed.Id.Should().Be("id");
        parsed.ProcessId.Should().Be(123);
        parsed.StateDirectory.Should().Be("/state=dir");
        parsed.LogFile.Should().Be("/state=dir/id.log");
    }

    [Fact]
    public async Task PublicManagerConstructors_SupportEmptyRegistry()
    {
        (await new CodexRemoteAppServerManager().ListAsync()).Should().BeEmpty();
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        await registry.UpsertAsync(DockerEntry("docker"));
        (await new CodexRemoteAppServerManager(registry).ListAsync()).Single().Id.Should().Be("docker");
    }
}
