using System.Diagnostics;
using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class CodexClientLiveResumeBootstrapTests
{
    [Fact]
    public async Task ResumeSessionAsync_ThrowsWhenExistingLogNeverAdvances()
    {
        using var fixture = new ResumeFixture();
        var sessionId = SessionId.Parse("11111111-1111-1111-1111-111111111111");
        fixture.WriteExistingLog(
            sessionId,
            """
            {"timestamp":"2026-04-01T12:00:00Z","type":"session_meta","payload":{"id":"11111111-1111-1111-1111-111111111111","cwd":"C:\\repo"}}
            """
        );

        var selectedSession = new CodexSessionInfo(
            sessionId,
            fixture.LogPath,
            DateTimeOffset.Parse("2026-04-01T12:00:00Z"),
            fixture.WorkingDirectory);

        using var process = SilentProcessFactory.CreateShortLivedProcess();
        using var client = new CodexClient(
            Options.Create(new CodexClientOptions
            {
                SessionsRootDirectory = fixture.SessionsRoot,
                StartTimeout = TimeSpan.FromMilliseconds(500)
            }),
            new StubProcessLauncher(process),
            new StubSessionLocator(selectedSession),
            new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, Options.Create(new CodexClientOptions())),
            new JsonlEventParser(NullLogger<JsonlEventParser>.Instance),
            new StubPathProvider(fixture.SessionsRoot),
            NullLogger<CodexClient>.Instance,
            NullLoggerFactory.Instance);

        var options = new CodexSessionOptions(fixture.WorkingDirectory, "resume prompt");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResumeSessionAsync(CodexResumeTarget.BySelector(sessionId.Value), options));

        Assert.Contains("rollout log advanced", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ResumeFixture : IDisposable
    {
        private readonly string _root;

        public ResumeFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), $"codex-live-resume-{Guid.NewGuid():N}");
            SessionsRoot = Path.Combine(_root, "sessions");
            WorkingDirectory = Path.Combine(_root, "repo");
            Directory.CreateDirectory(SessionsRoot);
            Directory.CreateDirectory(WorkingDirectory);
        }

        public string SessionsRoot { get; }

        public string WorkingDirectory { get; }

        public string LogPath { get; private set; } = string.Empty;

        public void WriteExistingLog(SessionId sessionId, string contents)
        {
            LogPath = Path.Combine(
                SessionsRoot,
                "2026",
                "04",
                "01",
                $"rollout-2026-04-01T12-00-00-{sessionId.Value}.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath, contents + Environment.NewLine);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Best-effort fixture cleanup only.
            }
        }
    }

    private sealed class StubProcessLauncher(Process process) : ICodexProcessLauncher
    {
        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Process> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            Task.FromResult(process);

        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> TerminateProcessAsync(Process processToTerminate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!processToTerminate.HasExited)
            {
                processToTerminate.Kill(entireProcessTree: true);
            }

            return Task.FromResult(processToTerminate.ExitCode);
        }
    }

    private sealed class StubSessionLocator(CodexSessionInfo session) : ICodexSessionLocator
    {
        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken) =>
            Task.FromResult(session.LogPath);

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(session.LogPath);

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken) =>
            Task.FromResult(logFilePath);

        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
            string sessionsRoot,
            SessionFilter? filter,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return session;
            await Task.CompletedTask;
        }
    }

    private sealed class StubPathProvider(string sessionsRoot) : ICodexPathProvider
    {
        public string GetCodexExecutablePath(string? overridePath) => overridePath ?? "codex";

        public string GetSessionsRootDirectory(string? overrideDirectory) => overrideDirectory ?? sessionsRoot;

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRootOverride) =>
            throw new NotSupportedException();
    }

    private static class SilentProcessFactory
    {
        public static Process CreateShortLivedProcess()
        {
            var startInfo = OperatingSystem.IsWindows()
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit 0",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-lc \"exit 0\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            return Process.Start(startInfo)!;
        }
    }
}
