using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ProcessLauncherCoverageTests
{
    private static string Executable => Path.ChangeExtension(typeof(ProcessFixture.Program).Assembly.Location, OperatingSystem.IsWindows() ? ".exe" : null);
    private static CodexProcessLauncher Launcher(string? executable = null) => new(new Paths(executable ?? Executable), NullLogger<CodexProcessLauncher>.Instance);

    [Theory]
    [InlineData("start", false, "prompt α")]
    [InlineData("resume", false, "prompt α")]
    [InlineData("review", false, "prompt α")]
    [InlineData("start", true, "payload β")]
    [InlineData("resume", true, "")]
    public async Task LaunchModes_WriteExactStdinAndClose(string operation, bool argumentMode, string expected)
    {
        using var process = await LaunchAsync(Launcher(), operation, argumentMode);
        (await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(expected);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        process.ExitCode.Should().Be(0);
        (await Launcher().TerminateProcessAsync(process, TimeSpan.FromSeconds(1), default)).Should().Be(0);
    }

    [Fact]
    public async Task ReviewWithoutPrompt_ClosesStdin()
    {
        using var process = await Launcher().StartReviewAsync(new CodexReviewOptions { WorkingDirectory = Path.GetTempPath(), Uncommitted = true }, new(), default);
        (await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10))).Should().BeEmpty();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("resume")]
    [InlineData("review")]
    public async Task MissingExecutable_IsWrappedWithDiagnosticAndCause(string operation)
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LaunchAsync(Launcher(missing), operation, false));
        ex.Message.Should().Contain(missing).And.Contain("See inner exception");
        ex.InnerException.Should().BeOfType<System.ComponentModel.Win32Exception>();
    }

    [Theory]
    [InlineData("start")]
    [InlineData("resume")]
    [InlineData("review")]
    public async Task CallerCancellationDuringBlockedInput_IsPropagated(string operation)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LaunchAsync(Launcher(), operation, false, "--fixture-wait", new string('x', 4 * 1024 * 1024), cts.Token));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("resume")]
    [InlineData("review")]
    public async Task ProcessExitingDuringInput_ReportsWriteFailure(string operation)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LaunchAsync(Launcher(), operation, false, "--fixture-exit", new string('x', 4 * 1024 * 1024)));
        ex.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("stdin");
    }

    [Theory]
    [InlineData("echo")]
    [InlineData("wait")]
    public async Task Terminate_ClosesStdinThenKillsIfNeeded(string mode)
    {
        var launch = ProcessLifetimeCoverageTests.Fixture(mode == "echo" ? "ready-echo" : mode);
        var info = new ProcessStartInfo(launch.FileName!) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in launch.Arguments) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        try
        {
            if (mode == "echo")
                (await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))).Should().Be("ready");
            var code = await Launcher().TerminateProcessAsync(process, TimeSpan.FromMilliseconds(250), default);
            process.HasExited.Should().BeTrue();
            code.Should().Be(process.ExitCode);
            if (mode == "echo") code.Should().Be(0);
            else code.Should().NotBe(0);
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    [Fact]
    public async Task StdioFactory_ResolvesExecutableAndForwardsLaunchSettings()
    {
        var provider = new Paths("dotnet");
        var factory = new StdioProcessFactory(provider, NullLogger<StdioProcessFactory>.Instance);
        var launch = ProcessLifetimeCoverageTests.Fixture("echo") with { FileName = null, WorkingDirectory = Path.GetTempPath() };
        await using var process = await factory.StartAsync(launch, "override", null, TimeSpan.FromMilliseconds(200), default);
        provider.Override.Should().Be("override");
        factory.PathProvider.Should().BeSameAs(provider);
        await process.Stdin.WriteAsync("factory");
        process.Stdin.Close();
        (await process.Stdout.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be("factory");
    }

    [Fact]
    public async Task StderrDiagnostics_HandlesUnstartedAndNonRedirectedProcesses()
    {
        using var unstarted = new Process();
        (await CodexProcessLauncherIo.TryReadStandardErrorAsync(unstarted)).Should().BeNull();
        unstarted.StartInfo.RedirectStandardError = true;
        (await CodexProcessLauncherIo.TryReadStandardErrorAsync(unstarted)).Should().BeNull();
        CodexProcessLauncherDiagnostics.CreateDiagnosticMessage("detail", "exe").Should().Contain("detail").And.Contain("exe");
    }

    [Fact]
    public async Task AppServerStartup_ForwardsConfiguredCodexHomeIntoRealChild()
    {
        var home = Path.Combine(Path.GetTempPath(), "codex-home-" + Guid.NewGuid());
        var options = new CodexAppServerClientOptions
        {
            Launch = ProcessLifetimeCoverageTests.Fixture("appserver").WithEnvironment("CODEX_HOME", "overridden"),
            CodexHomeDirectory = home,
            StartupTimeout = TimeSpan.FromSeconds(10),
            ShutdownTimeout = TimeSpan.FromSeconds(2)
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await CodexAppServerClient.StartAsync(options, deadline.Token);
        client.InitializeResult!.Raw.GetProperty("codexHome").GetString().Should().Be(home);
        client.InitializeResult.UserAgent.Should().Be("process-fixture");
        options.Launch.Environment["CODEX_HOME"].Should().Be("overridden");
    }

    private static Task<Process> LaunchAsync(CodexProcessLauncher launcher, string operation, bool argumentMode, string? flag = null, string prompt = "prompt α", CancellationToken ct = default)
    {
        var options = argumentMode
            ? new CodexSessionOptions { WorkingDirectory = Path.GetTempPath(), PromptArgument = "instructions", StdinPayload = "payload β" }
            : new CodexSessionOptions(Path.GetTempPath(), prompt);
        options.AdditionalOptions = flag is null ? [] : [flag];
        return operation switch
        {
            "start" => launcher.StartSessionAsync(options, new(), ct),
            "resume" => launcher.ResumeSessionAsync(SessionId.Parse("12345678-1234-1234-1234-123456789abc"), options, new(), ct),
            _ => launcher.StartReviewAsync(new CodexReviewOptions { WorkingDirectory = Path.GetTempPath(), Prompt = prompt, AdditionalOptions = options.AdditionalOptions }, new(), ct)
        };
    }

    private sealed class Paths(string executable) : ICodexPathProvider
    {
        public string? Override { get; private set; }
        public string GetCodexExecutablePath(string? overridePath) { Override = overridePath; return executable; }
        public string GetSessionsRootDirectory(string? overrideDirectory) => throw new NotSupportedException();
        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) => throw new NotSupportedException();
    }
}
