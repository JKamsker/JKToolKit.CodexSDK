using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ProcessLifetimeCoverageTests
{
    internal static CodexLaunch Fixture(string mode) => new()
    {
        FileName = "dotnet",
        Arguments = [typeof(ProcessFixture.Program).Assembly.Location, mode]
    };

    internal static ProcessLaunchOptions Options(string mode) => new()
    {
        ResolvedFileName = "dotnet",
        Arguments = Fixture(mode).Arguments,
        StartupTimeout = TimeSpan.FromSeconds(10),
        ShutdownTimeout = TimeSpan.FromMilliseconds(200)
    };

    [Fact]
    public async Task RemoteRun_CapturesExitCodeUnicodeEnvironmentAndWorkingDirectory()
    {
        var launch = Fixture("output").WithEnvironment("CODEX_TEST_VALUE", "hello world").WithWorkingDirectory(Path.GetTempPath());
        var result = await new RemoteProcessRunner(NullLogger.Instance).RunAsync(launch, Timeout.InfiniteTimeSpan, default);
        result.ExitCode.Should().Be(7);
        result.StandardOutput.Should().Be($"hello world|{Path.TrimEndingDirectorySeparator(Path.GetTempPath())}|α");
        result.StandardError.Should().Be("stderr-β");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteRun_CancellationAndTimeoutKillStartedProcess(bool timeout)
    {
        var marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pid");
        using var cts = new CancellationTokenSource();
        Process? child = null;
        try
        {
            var task = new RemoteProcessRunner(NullLogger.Instance).RunAsync(
                Fixture("wait").WithEnvironment("CODEX_TEST_MARKER", marker),
                timeout ? TimeSpan.FromSeconds(2) : Timeout.InfiniteTimeSpan, cts.Token);
            await WaitForMarkerAsync(marker);
            child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker)));
            if (!timeout) cts.Cancel();
            if (timeout) await Assert.ThrowsAsync<TimeoutException>(() => task);
            else (await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task)).CancellationToken.Should().Be(cts.Token);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            child.HasExited.Should().BeTrue();
        }
        finally
        {
            if (child is not null)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                child.Dispose();
            }
            File.Delete(marker);
        }
    }

    [Fact]
    public async Task AlreadyCanceledLaunches_DoNotAttemptToStartExecutable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new RemoteProcessRunner(NullLogger.Instance);
        var missing = new CodexLaunch { FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(missing, TimeSpan.FromSeconds(1), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.StartAsync(missing, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StdioProcess.StartAsync(
            Options("wait") with { ResolvedFileName = missing.FileName! }, NullLogger.Instance, cts.Token));
    }

    [Fact]
    public async Task RemoteStart_DisposalKillsProcessAndIsIdempotent()
    {
        var process = await new RemoteProcessRunner(NullLogger.Instance).StartAsync(Fixture("wait"), default);
        process.Completion.IsCompleted.Should().BeFalse();
        await process.DisposeAsync();
        await process.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await process.DisposeAsync();
    }

    [Fact]
    public async Task RemoteRun_RequiresExplicitExecutable()
    {
        var runner = new RemoteProcessRunner(NullLogger.Instance);
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null!, TimeSpan.FromSeconds(1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(new CodexLaunch(), TimeSpan.FromSeconds(1), default));
    }

    [Fact]
    public async Task Stdio_EchoesInputExposesLifecycleAndDisposesTwice()
    {
        var process = await StdioProcess.StartAsync(Options("echo"), NullLogger.Instance, default);
        var id = process.ProcessId;
        try
        {
            id.Should().BePositive();
            process.ExitCode.Should().BeNull();
            await process.Stdin.WriteAsync("hello α");
            process.Stdin.Close();
            (await process.Stdout.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be("hello α");
            await process.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            process.ExitCode.Should().Be(0);
        }
        finally { await process.DisposeAsync(); }
        process.ProcessId.Should().BeNull();
        process.ExitCode.Should().BeNull();
        await process.DisposeAsync();
    }

    [Fact]
    public async Task Stdio_BoundsStderrTailAndLongLines()
    {
        await using var process = await StdioProcess.StartAsync(Options("stderr"), NullLogger.Instance, default);
        (await process.Stdout.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be("ready");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (process.StderrTail.LastOrDefault()?.EndsWith('…') != true) await Task.Delay(10, deadline.Token);
        var tail = process.StderrTail;
        tail.Should().HaveCount(200);
        tail[0].Should().Be("line-6");
        tail[^2].Should().Be("line-204");
        tail[^1].Should().Be(new string('x', 4096) + "…");
    }

    [Fact]
    public async Task Stdio_DisposalKillsUnresponsiveProcess()
    {
        var process = await StdioProcess.StartAsync(Options("wait"), NullLogger.Instance, default);
        using var observed = Process.GetProcessById(process.ProcessId!.Value);
        await process.DisposeAsync();
        await observed.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Stdio_MissingExecutablePreservesCause()
    {
        var options = Options("echo") with { ResolvedFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StdioProcess.StartAsync(options, NullLogger.Instance, default));
        ex.Message.Should().Contain(options.ResolvedFileName);
        ex.InnerException.Should().BeOfType<System.ComponentModel.Win32Exception>();
    }

    internal static async Task WaitForMarkerAsync(string marker)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(marker) || new FileInfo(marker).Length == 0) await Task.Delay(10, deadline.Token);
    }
}
