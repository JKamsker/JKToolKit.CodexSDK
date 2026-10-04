using System.Diagnostics;
using System.IO;
using System;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Integration;

public class CodexClientStartSessionTests
{
    private static readonly ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public async Task StartSessionAsync_StreamsEventsInOrder_AndReturnsSessionId()
    {
        // Arrange
        var startTimeout = TimeSpan.FromSeconds(2);
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var clientOptions = Options.Create(new CodexClientOptions { StartTimeout = startTimeout });
        var sessionOptions = new CodexSessionOptions(workingDirectory: workingDirectory, prompt: "hello world");

        var sessionId = SessionId.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var escapedWorkingDirectory = workingDirectory.Replace("\\", "\\\\");
        var rolloutTimestamp = DateTimeOffset.Parse("2025-11-20T22:00:00Z");
        var lines = new[]
        {
            $"{{\"timestamp\":\"2025-11-20T22:00:00Z\",\"type\":\"session_meta\",\"payload\":{{\"id\":\"{sessionId}\",\"cwd\":\"{escapedWorkingDirectory}\"}}}}",
            """{"timestamp":"2025-11-20T22:00:01Z","type":"user_message","payload":{"message":"hello world"}}""",
            """{"timestamp":"2025-11-20T22:00:02Z","type":"agent_reasoning","payload":{"text":"thinking"}}""",
            """{"timestamp":"2025-11-20T22:00:03Z","type":"agent_message","payload":{"message":"hi!"}}""",
            """{"timestamp":"2025-11-20T22:00:04Z","type":"token_count","payload":{"input_tokens":10,"output_tokens":5,"reasoning_output_tokens":2}}"""
        };

        using var process = FakeProcessLauncher.CreateLongLivedProcess(sessionId.Value);

        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator(SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", rolloutTimestamp, sessionId));
        var tailer = new FakeTailer(lines);
        var parser = new JKToolKit.CodexSDK.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<JKToolKit.CodexSDK.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        try
        {
            // Act
            var sw = Stopwatch.StartNew();
            await using var handle = await client.StartSessionAsync(sessionOptions);
            sw.Stop();

            // Assert session
            Assert.Equal(sessionId, handle.Info.Id);
            Assert.True(handle.IsLive);
            // Assert events order
            var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();
            Assert.Equal(5, events.Count);
            Assert.Collection(events,
                e => Assert.IsType<SessionMetaEvent>(e),
                e => Assert.IsType<UserMessageEvent>(e),
                e => Assert.IsType<AgentReasoningEvent>(e),
                e => Assert.IsType<AgentMessageEvent>(e),
                e => Assert.IsType<TokenCountEvent>(e));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartSessionAsync_Fails_WhenProcessExitsBeforeSessionMeta()
    {
        // Arrange
        var clientOptions = Options.Create(new CodexClientOptions
        {
            StartTimeout = TimeSpan.FromSeconds(2),
            EnableUncorrelatedNewSessionFileDiscovery = true
        });
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var sessionOptions = new CodexSessionOptions(workingDirectory, "prompt");

        using var process = FakeProcessLauncher.CreateShortProcess(); // exits almost immediately

        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator(
            SessionLogPathTestHelper.BuildNestedRolloutPath(
                "C:\\sessions",
                DateTimeOffset.Parse("2025-11-20T22:15:00Z"),
                SessionId.Parse("session-123")));
        var tailer = new FakeTailer(Array.Empty<string>()); // no session_meta
        var parser = new JKToolKit.CodexSDK.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<JKToolKit.CodexSDK.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        // Act + Assert
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartSessionAsync(sessionOptions));
            Assert.Contains("session_meta", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartSessionAsync_Fails_WhenSessionLogNotFound()
    {
        // Arrange
        var clientOptions = Options.Create(new CodexClientOptions { StartTimeout = TimeSpan.FromSeconds(1) });
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var sessionOptions = new CodexSessionOptions(workingDirectory, "prompt");

        var launcher = new FakeProcessLauncher(FakeProcessLauncher.CreateLongLivedProcess());
        var locator = new FakeSessionLocator(throwOnWait: true);
        var tailer = new FakeTailer(Array.Empty<string>());
        var parser = new JKToolKit.CodexSDK.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<JKToolKit.CodexSDK.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        try
        {
            // Act + Assert
            await Assert.ThrowsAnyAsync<Exception>(() => client.StartSessionAsync(sessionOptions));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StartFailure_WithAndWithoutCapturedId_PreservesDiscoveryCauseAndCleansSchema(bool emitId, bool fallback)
    {
        using var process = emitId ? FakeProcessLauncher.CreateLongLivedProcess("captured") : FakeProcessLauncher.CreateShortProcess();
        var launcher = new FakeProcessLauncher(process);
        var original = new IOException("lookup-failure");
        var locator = new FakeSessionLocator(throwOnWait: true) { LookupFailure = original };
        var clientOptions = Options.Create(new CodexClientOptions
        {
            StartTimeout = TimeSpan.FromMilliseconds(250), EnableUncorrelatedNewSessionFileDiscovery = fallback,
            EnableDiagnosticCapture = true
        });
        using var client = new CodexClient(clientOptions, launcher, locator, new FakeTailer([]),
            pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        var sessionOptions = new CodexSessionOptions(Path.GetTempPath(), "prompt")
        {
            OutputSchema = JKToolKit.CodexSDK.StructuredOutputs.CodexOutputSchema.FromJson(System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }))
        };
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.StartSessionAsync(sessionOptions));
        if (emitId && fallback) Assert.IsType<TimeoutException>(failure);
        else
        {
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains("Failed to locate", failure.Message);
            Assert.Contains("redacted", failure.Message);
            if (emitId) Assert.Same(original, failure.InnerException);
            else if (fallback) Assert.IsType<TimeoutException>(failure.InnerException);
        }
        Assert.NotNull(launcher.LastOptions!.OutputSchema);
        Assert.False(File.Exists(launcher.LastOptions.OutputSchema!.FilePath));
        Assert.True(launcher.TerminateCalls > 0);
    }

    [Fact]
    public async Task CapturedIdLookupFailure_UsesOptedInDiscoveryAndMetadata()
    {
        using var process = FakeProcessLauncher.CreateLongLivedProcess("captured");
        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator("fallback.jsonl") { LookupFailure = new IOException("missing captured log") };
        using var client = new CodexClient(Options.Create(new CodexClientOptions { EnableUncorrelatedNewSessionFileDiscovery = true }),
            launcher, locator, new FakeTailer(["""{"timestamp":"2025-11-20T22:00:00Z","type":"session_meta","payload":{"id":"discovered"}}"""]),
            pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        await using var handle = await client.StartSessionAsync(new(Path.GetTempPath(), "prompt"));
        Assert.Equal("discovered", handle.Info.Id.Value);
        Assert.Equal("fallback.jsonl", handle.Info.LogPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataWait_DistinguishesTimeoutFromProcessExit_AndCleansProcess(bool exitEarly)
    {
        using var process = exitEarly ? FakeProcessLauncher.CreateShortProcess() : FakeProcessLauncher.CreateLongLivedProcess("captured");
        var launcher = new FakeProcessLauncher(process);
        using var client = new CodexClient(Options.Create(new CodexClientOptions
        {
            StartTimeout = TimeSpan.FromMilliseconds(100), EnableUncorrelatedNewSessionFileDiscovery = true
        }), launcher, new FakeSessionLocator("log"), new FakeTailer([]) { WaitAfterLines = true },
            pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        var start = () => client.StartSessionAsync(new(Path.GetTempPath(), "prompt"));
        if (exitEarly)
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(start);
            Assert.Contains("exited with code 0", failure.Message);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<TimeoutException>(start);
            Assert.Contains("session_meta", failure.Message);
        }
        Assert.True(launcher.TerminateCalls > 0);
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("lookup")]
    [InlineData("discovery")]
    public async Task StartCancellation_PropagatesAtEveryDiscoveryPhase_AndTerminatesChild(string phase)
    {
        using var cancellation = new CancellationTokenSource();
        using var process = phase == "capture" ? FakeProcessLauncher.CreateSilentProcess() : phase == "lookup"
            ? FakeProcessLauncher.CreateLongLivedProcess("captured") : FakeProcessLauncher.CreateShortProcess();
        using var observer = phase == "discovery" ? null : Process.GetProcessById(process.Id);
        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator("log");
        if (phase == "capture") launcher.AfterStart = cancellation.Cancel;
        if (phase == "lookup") locator.LookupOverride = token => { cancellation.Cancel(); return Task.FromCanceled<string>(token); };
        if (phase == "discovery") locator.DiscoveryOverride = token => CancelDiscoveryAsync(token);
        using var client = new CodexClient(Options.Create(new CodexClientOptions { EnableUncorrelatedNewSessionFileDiscovery = phase == "discovery" }),
            launcher, locator, new FakeTailer([]), pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.StartSessionAsync(new(Path.GetTempPath(), "prompt"), cancellation.Token));
        Assert.Equal(1, launcher.TerminateCalls);
        if (observer is not null) await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        async Task<string> CancelDiscoveryAsync(CancellationToken token)
        {
            await Task.Delay(300);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return "unreachable";
        }
    }

    [Fact]
    public async Task SessionIdCaptureTimeout_UsesOptedInDiscovery_AfterSilentChildProducesNoId()
    {
        using var process = FakeProcessLauncher.CreateSilentProcess();
        var launcher = new FakeProcessLauncher(process);
        using var client = new CodexClient(Options.Create(new CodexClientOptions
        {
            StartTimeout = TimeSpan.FromMilliseconds(100), EnableUncorrelatedNewSessionFileDiscovery = true
        }), launcher, new FakeSessionLocator("discovered-log"),
            new FakeTailer(["""{"timestamp":"2025-11-20T22:00:00Z","type":"session_meta","payload":{"id":"discovered"}}"""]),
            pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var handle = await client.StartSessionAsync(new(Path.GetTempPath(), "prompt"), deadline.Token);
        Assert.Equal("discovered", handle.Info.Id.Value);
        Assert.Equal("discovered-log", handle.Info.LogPath);
    }

    [Fact]
    public async Task FailedGracefulCleanup_StillKillsChild_AndPreservesOriginalStartFailure()
    {
        using var process = FakeProcessLauncher.CreateLongLivedProcess("captured");
        using var observer = Process.GetProcessById(process.Id);
        var launcher = new FakeProcessLauncher(process) { TerminationFailure = new IOException("termination failed") };
        using var client = new CodexClient(Options.Create(new CodexClientOptions()), launcher,
            new FakeSessionLocator("log") { LookupFailure = new IOException("lookup failed") }, new FakeTailer([]),
            pathProvider: new FakePathProvider(Path.GetTempPath()), loggerFactory: LoggerFactory);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartSessionAsync(new(Path.GetTempPath(), "prompt")));
        Assert.Equal("lookup failed", failure.InnerException!.Message);
        await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, launcher.TerminateCalls);
    }

    private sealed class FakeProcessLauncher : ICodexProcessLauncher
    {
        private readonly Process _process;
        public CodexSessionOptions? LastOptions;
        public int TerminateCalls;
        public Action? AfterStart;
        public Exception? TerminationFailure;

        public FakeProcessLauncher(Process process)
        {
            _process = process;
        }

        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken)
        {
            LastOptions = options;
            AfterStart?.Invoke();
            return Task.FromResult(_process);
        }

        public Task<Process> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(_process);
        }

        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(_process);
        }

        public Task<int> TerminateProcessAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
        {
            TerminateCalls++;
            if (TerminationFailure is not null) throw TerminationFailure;
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            return Task.FromResult(process.ExitCode);
        }

        public static Process CreateLongLivedProcess(string? sessionId = null)
        {
            var isWindows = OperatingSystem.IsWindows();
            var sid = sessionId ?? "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

            // Keep the process alive for a short while so tests can interact with it
            var psi = isWindows
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c ping -n 2 127.0.0.1 >NUL & echo session id: {sid} & ping -n 30 127.0.0.1 >NUL",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"sleep 0.2; echo \\\"session id: {sid}\\\"; sleep 30\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

            return Process.Start(psi)!;
        }

        public static Process CreateSilentProcess()
        {
            var start = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(OperatingSystem.IsWindows() ? "-Command" : "-c");
            start.ArgumentList.Add(OperatingSystem.IsWindows() ? "[Console]::ReadLine() | Out-Null" : "read ignored");
            return Process.Start(start)!;
        }

        public static Process CreateShortProcess()
        {
            var isWindows = OperatingSystem.IsWindows();

            var psi = isWindows
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit 0",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"exit 0\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

            return Process.Start(psi)!;
        }
    }

    private sealed class FakeSessionLocator : ICodexSessionLocator
    {
        private readonly string _path;
        private readonly bool _throwOnWait;
        public Exception? LookupFailure;
        public Func<CancellationToken, Task<string>>? LookupOverride;
        public Func<CancellationToken, Task<string>>? DiscoveryOverride;

        public FakeSessionLocator(string path)
        {
            _path = path;
        }

        public FakeSessionLocator(bool throwOnWait)
        {
            _throwOnWait = throwOnWait;
            _path = string.Empty;
        }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (DiscoveryOverride is not null) return DiscoveryOverride(cancellationToken);
            if (_throwOnWait)
            {
                throw new TimeoutException("No session file");
            }

            return Task.FromResult(_path);
        }

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (LookupOverride is not null) return LookupOverride(cancellationToken);
            return LookupFailure is null ? Task.FromResult(_path) : Task.FromException<string>(LookupFailure);
        }

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(logFilePath);
        }

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string sessionsRoot, SessionFilter? filter, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeTailer : IJsonlTailer
    {
        private readonly IReadOnlyList<string> _lines;
        public bool WaitAfterLines;

        public FakeTailer(IEnumerable<string> lines)
        {
            _lines = lines.ToList();
        }

        public async IAsyncEnumerable<string> TailAsync(string filePath, EventStreamOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var line in _lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
            if (WaitAfterLines) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class FakePathProvider : ICodexPathProvider
    {
        private readonly string _sessionsRoot;

        public FakePathProvider(string sessionsRoot)
        {
            _sessionsRoot = sessionsRoot;
        }

        public string GetCodexExecutablePath(string? overridePath) =>
            overridePath ?? "codex.exe";

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            overrideDirectory ?? _sessionsRoot;

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            SessionLogPathTestHelper.BuildNestedRolloutPath(
                sessionsRoot ?? _sessionsRoot,
                DateTimeOffset.Parse("2025-11-20T22:00:00Z"),
                sessionId);
    }
}
