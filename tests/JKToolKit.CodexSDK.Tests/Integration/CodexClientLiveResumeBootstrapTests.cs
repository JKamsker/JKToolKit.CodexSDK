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
        Assert.True(
            process.WaitForExit(1_000),
            "Expected the short-lived resume process fixture to exit before bootstrap monitoring starts.");
        using var client = new CodexClient(
            Options.Create(new CodexClientOptions
            {
                SessionsRootDirectory = fixture.SessionsRoot,
                StartTimeout = TimeSpan.FromMilliseconds(500)
            }),
            new StubProcessLauncher(process),
            new StubSessionLocator([selectedSession]),
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

    [Fact]
    public async Task ResumeSessionAsync_WhenTargetFallsBackToThreadStart_UsesUncorrelatedDiscovery()
    {
        using var fixture = new ResumeFixture();
        var newSessionId = SessionId.Parse("22222222-2222-2222-2222-222222222222");
        var newLogPath = fixture.WriteNewLog(
            newSessionId,
            "2026-04-01T12:05:00Z",
            """
            {"timestamp":"2026-04-01T12:05:00Z","type":"session_meta","payload":{"id":"22222222-2222-2222-2222-222222222222","cwd":"C:\\repo"}}
            {"timestamp":"2026-04-01T12:05:01Z","type":"agent_message","payload":{"message":"started fresh"}}
            """
        );

        using var process = SilentProcessFactory.CreateLongLivedSilentProcess();
        using var client = new CodexClient(
            Options.Create(new CodexClientOptions
            {
                SessionsRootDirectory = fixture.SessionsRoot,
                StartTimeout = TimeSpan.FromMilliseconds(500),
                EnableUncorrelatedNewSessionFileDiscovery = true
            }),
            new StubProcessLauncher(process),
            new StubSessionLocator(
                sessions: Array.Empty<CodexSessionInfo>(),
                newSessionFilePath: newLogPath),
            new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, Options.Create(new CodexClientOptions())),
            new JsonlEventParser(NullLogger<JsonlEventParser>.Instance),
            new StubPathProvider(fixture.SessionsRoot),
            NullLogger<CodexClient>.Instance,
            NullLoggerFactory.Instance);

        var options = new CodexSessionOptions(fixture.WorkingDirectory, "resume prompt");
        await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.MostRecent(), options);

        Assert.Equal(newSessionId, handle.Info.Id);
        Assert.Equal(newLogPath, handle.Info.LogPath);
        Assert.Contains("started fresh", File.ReadAllText(newLogPath));
    }

    [Fact]
    public async Task ResumeSessionAsync_WhenCapturedIdCannotBeResolved_FallsBackToUncorrelatedDiscovery()
    {
        using var fixture = new ResumeFixture();
        var newSessionId = SessionId.Parse("33333333-3333-3333-3333-333333333333");
        var newLogPath = fixture.WriteNewLog(
            newSessionId,
            "2026-04-01T12:10:00Z",
            """
            {"timestamp":"2026-04-01T12:10:00Z","type":"session_meta","payload":{"id":"33333333-3333-3333-3333-333333333333","cwd":"C:\\repo"}}
            {"timestamp":"2026-04-01T12:10:01Z","type":"agent_message","payload":{"message":"captured then recovered"}}
            """
        );

        using var process = SilentProcessFactory.CreateLongLivedProcessWithSessionId(newSessionId.Value);
        using var client = new CodexClient(
            Options.Create(new CodexClientOptions
            {
                SessionsRootDirectory = fixture.SessionsRoot,
                StartTimeout = TimeSpan.FromMilliseconds(800),
                EnableUncorrelatedNewSessionFileDiscovery = true
            }),
            new StubProcessLauncher(process),
            new StubSessionLocator(
                sessions: Array.Empty<CodexSessionInfo>(),
                newSessionFilePath: newLogPath,
                throwOnWaitById: true),
            new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, Options.Create(new CodexClientOptions())),
            new JsonlEventParser(NullLogger<JsonlEventParser>.Instance),
            new StubPathProvider(fixture.SessionsRoot),
            NullLogger<CodexClient>.Instance,
            NullLoggerFactory.Instance);

        var options = new CodexSessionOptions(fixture.WorkingDirectory, "resume prompt");
        await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.MostRecent(), options);

        Assert.Equal(newSessionId, handle.Info.Id);
        Assert.Equal(newLogPath, handle.Info.LogPath);
        Assert.Contains("captured then recovered", File.ReadAllText(newLogPath));
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
            LogPath = WriteNewLog(sessionId, "2026-04-01T12:00:00Z", contents);
        }

        public string WriteNewLog(SessionId sessionId, string timestamp, string contents)
        {
            var parsedTimestamp = DateTimeOffset.Parse(timestamp);
            var logPath = Path.Combine(
                SessionsRoot,
                parsedTimestamp.ToString("yyyy"),
                parsedTimestamp.ToString("MM"),
                parsedTimestamp.ToString("dd"),
                $"rollout-{parsedTimestamp:yyyy-MM-ddTHH-mm-ss}-{sessionId.Value}.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, contents + Environment.NewLine);
            LogPath = logPath;
            return logPath;
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

    private sealed class StubSessionLocator(
        IReadOnlyList<CodexSessionInfo> sessions,
        string? newSessionFilePath = null,
        bool throwOnWaitById = false) : ICodexSessionLocator
    {
        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken) =>
            newSessionFilePath is null
                ? throw new TimeoutException("No fallback session file was discovered.")
                : Task.FromResult(newSessionFilePath);

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken) =>
            Task.FromResult(ResolveSessionLogPath(sessionId));

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken) =>
            throwOnWaitById
                ? throw new TimeoutException("Timed out locating session log by captured id.")
                : Task.FromResult(ResolveSessionLogPath(sessionId));

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken) =>
            Task.FromResult(logFilePath);

        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
            string sessionsRoot,
            SessionFilter? filter,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var session in sessions)
            {
                yield return session;
                await Task.CompletedTask;
            }
        }

        private string ResolveSessionLogPath(SessionId sessionId)
        {
            var session = sessions.FirstOrDefault(candidate => candidate.Id.Equals(sessionId));
            if (session is not null)
            {
                return session.LogPath;
            }

            if (newSessionFilePath is not null)
            {
                return newSessionFilePath;
            }

            throw new FileNotFoundException("Session log not found.", sessionId.Value);
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

        public static Process CreateLongLivedSilentProcess()
        {
            var startInfo = OperatingSystem.IsWindows()
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping -n 30 127.0.0.1 >NUL",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-lc \"sleep 30\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            return Process.Start(startInfo)!;
        }

        public static Process CreateLongLivedProcessWithSessionId(string sessionId)
        {
            var startInfo = OperatingSystem.IsWindows()
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c ping -n 2 127.0.0.1 >NUL & echo session id: {sessionId} & ping -n 30 127.0.0.1 >NUL",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-lc \"sleep 0.2; echo \\\"session id: {sessionId}\\\"; sleep 30\"",
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
