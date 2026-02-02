using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Public;
using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class CodexClientReviewStreamingTests
{
    [Fact]
    public async Task ReviewAsync_MirrorsStdoutAndStderr_WhileRunning()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var reviewOptions = new CodexReviewOptions(workingDirectory);

        using var process = CreateEchoThenSleepProcess();
        var launcher = new ReviewProcessLauncher(process);
        var client = new CodexClient(
            Options.Create(new CodexClientOptions()),
            launcher,
            logger: NullLogger<CodexClient>.Instance,
            loggerFactory: NullLoggerFactory.Instance);

        var stdout = new SignalTextWriter();
        var stderr = new SignalTextWriter();

        var reviewTask = client.ReviewAsync(reviewOptions, stdout, stderr, CancellationToken.None);

        await stdout.FirstWrite.WaitAsync(TimeSpan.FromSeconds(2));
        await stderr.FirstWrite.WaitAsync(TimeSpan.FromSeconds(2));

        var result = await reviewTask;

        stdout.Buffer.ToString().Should().Contain("OUT");
        stderr.Buffer.ToString().Should().Contain("ERR");

        result.StandardOutput.Should().Contain("OUT");
        result.StandardError.Should().Contain("ERR");
    }

    [Fact]
    public async Task ReviewAsync_ExtractsSessionId_AndResolvesLogPath()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var reviewOptions = new CodexReviewOptions(workingDirectory);

        var sessionId = SessionId.Parse("11111111-1111-1111-1111-111111111111");
        using var process = CreateEchoSessionIdProcess(sessionId);

        var sessionsRoot = Path.Combine(Path.GetTempPath(), "JKToolKit.CodexSDK.Tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(sessionsRoot);
        var expectedLogPath = Path.Combine(sessionsRoot, $"rollout-{sessionId.Value}.jsonl");

        var launcher = new ReviewProcessLauncher(process);
        var locator = new FakeSessionLocator(expectedLogPath);
        var pathProvider = new FakePathProvider(sessionsRoot);

        var clientOptions = new CodexClientOptions { SessionsRootDirectory = sessionsRoot };
        var client = new CodexClient(
            Options.Create(clientOptions),
            launcher,
            sessionLocator: locator,
            pathProvider: pathProvider,
            logger: NullLogger<CodexClient>.Instance,
            loggerFactory: NullLoggerFactory.Instance);

        var result = await client.ReviewAsync(reviewOptions, CancellationToken.None);

        result.SessionId.Should().Be(sessionId);
        result.LogPath.Should().Be(expectedLogPath);
    }

    private static Process CreateEchoThenSleepProcess()
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo OUT & echo ERR 1>&2 & ping -n 3 127.0.0.1 >NUL",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"echo OUT; echo ERR 1>&2; sleep 2\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start test process.");
    }

    private static Process CreateEchoSessionIdProcess(SessionId sessionId)
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c echo session id: {sessionId.Value} & echo OUT & echo ERR 1>&2",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"echo 'session id: {sessionId.Value}'; echo OUT; echo ERR 1>&2\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start test process.");
    }

    private sealed class ReviewProcessLauncher(Process process) : ICodexProcessLauncher
    {
        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Process> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            Task.FromResult(process);

        public Task<int> TerminateProcessAsync(Process processToTerminate, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(processToTerminate.ExitCode);
    }

    private sealed class FakeSessionLocator(string logPath) : ICodexSessionLocator
    {
        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken) =>
            Task.FromResult(logPath);

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(logPath);

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken) =>
            Task.FromResult(logFilePath);

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string sessionsRoot, SessionFilter? filter, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakePathProvider(string sessionsRoot) : ICodexPathProvider
    {
        public string GetCodexExecutablePath(string? overridePath) =>
            overridePath ?? "codex";

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            overrideDirectory ?? sessionsRoot;

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRootOverride) =>
            Path.Combine(sessionsRootOverride ?? sessionsRoot, $"rollout-{sessionId.Value}.jsonl");
    }

    private sealed class SignalTextWriter : TextWriter
    {
        private readonly TaskCompletionSource _firstWriteTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StringBuilder Buffer { get; } = new();
        public Task FirstWrite => _firstWriteTcs.Task;

        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!_firstWriteTcs.Task.IsCompleted)
            {
                _firstWriteTcs.TrySetResult();
            }

            Buffer.Append(buffer.Span);
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
