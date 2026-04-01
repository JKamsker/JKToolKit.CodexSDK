using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Integration;

[Collection("CurrentDirectory")]
public class CodexClientResumeSessionTests
{
    private static readonly ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public async Task ResumeSessionAsync_ReplaysAllEvents_FromBeginning()
    {
        // Arrange
        var baseTime = DateTimeOffset.Parse("2025-11-20T22:00:00Z");
        var sessionId = SessionId.Parse("session-123");
        var logPath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime, sessionId);

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateUserMessage("hello", baseTime.AddSeconds(1)),
            TestJsonlGenerator.GenerateAgentReasoning("thinking", baseTime.AddSeconds(2)),
            TestJsonlGenerator.GenerateAgentMessage("hi!", baseTime.AddSeconds(3)),
            TestJsonlGenerator.GenerateTokenCount(10, 5, 2, baseTime.AddSeconds(4))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act
        await using var handle = await client.ResumeSessionAsync(sessionId);
        var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();

        // Assert
        Assert.False(handle.IsLive);
        Assert.Equal(logPath, handle.Info.LogPath);
        Assert.Equal(sessionId, handle.Info.Id);
        Assert.Equal(5, events.Count);
        Assert.Collection(events,
            e => Assert.IsType<SessionMetaEvent>(e),
            e => Assert.IsType<UserMessageEvent>(e),
            e => Assert.IsType<AgentReasoningEvent>(e),
            e => Assert.IsType<AgentMessageEvent>(e),
            e => Assert.IsType<TokenCountEvent>(e));
    }

    [Fact]
    public async Task AttachToLogAsync_AllowsReplayByPath()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-path");
        var baseTime = DateTimeOffset.Parse("2025-11-20T23:00:00Z");
        var logPath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime, sessionId);

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateAgentMessage("attached", baseTime.AddSeconds(1))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act
        await using var handle = await client.AttachToLogAsync(logPath);
        var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();

        // Assert
        Assert.False(handle.IsLive);
        Assert.Equal(logPath, handle.Info.LogPath);
        Assert.Equal(sessionId, handle.Info.Id);
        Assert.Equal(2, events.Count);
        Assert.IsType<SessionMetaEvent>(events[0]);
        Assert.IsType<AgentMessageEvent>(events[1]);
    }

    [Fact]
    public async Task ResumeSessionAsync_HonorsAfterTimestampFilter()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-filter");
        var baseTime = DateTimeOffset.Parse("2025-11-20T23:30:00Z");
        var logPath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime, sessionId);

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateUserMessage("first", baseTime.AddSeconds(1)),
            TestJsonlGenerator.GenerateAgentMessage("second", baseTime.AddSeconds(2))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        var filterOptions = new EventStreamOptions(FromBeginning: false, AfterTimestamp: baseTime.AddSeconds(1));

        // Act
        await using var handle = await client.ResumeSessionAsync(sessionId);
        var events = await handle.GetEventsAsync(filterOptions, CancellationToken.None).ToListAsync();

        // Assert
        var agentMessage = Assert.Single(events);
        var agentMessageEvent = Assert.IsType<AgentMessageEvent>(agentMessage);
        Assert.Equal("second", agentMessageEvent.Text);
    }

    [Fact]
    public async Task ResumeSessionAsync_ThrowsWhenSessionNotFound()
    {
        // Arrange
        var sessionId = SessionId.Parse("missing-session");
        var locator = new StubSessionLocator(path: string.Empty, throwOnFind: true);
        var options = Options.Create(new CodexClientOptions());
        var tailer = new FakeTailer(Array.Empty<string>());
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act + Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => client.ResumeSessionAsync(sessionId));
    }

    [Fact]
    public async Task ResumeSessionAsync_WithMostRecentTarget_ReplaysNewestSession()
    {
        var firstId = SessionId.Parse("session-first");
        var secondId = SessionId.Parse("session-second");
        var baseTime = DateTimeOffset.Parse("2025-11-21T00:00:00Z");
        var newestPath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime.AddMinutes(1), secondId);
        var cwd = Path.Combine(Path.GetTempPath(), $"codex-most-recent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cwd);

        try
        {
            var logs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [newestPath] = new[]
                {
                    TestJsonlGenerator.GenerateSessionMeta(secondId, cwd, baseTime.AddMinutes(1)),
                    TestJsonlGenerator.GenerateAgentMessage("newest", baseTime.AddMinutes(1).AddSeconds(1))
                }
            };

            var locator = new StubSessionLocator(
                newestPath,
                listedSessions:
                [
                    new CodexSessionInfo(firstId, "C:\\sessions\\older.jsonl", baseTime, cwd, null, "older"),
                    new CodexSessionInfo(secondId, newestPath, baseTime.AddMinutes(1), cwd, null, "newest")
                ]);

            var client = new CodexClient(
                Options.Create(new CodexClientOptions()),
                processLauncher: null,
                locator,
                new PathAwareTailer(logs),
                new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>()),
                new FakePathProvider("C:\\sessions"),
                LoggerFactory.CreateLogger<CodexClient>(),
                LoggerFactory);

            using var scope = new CurrentDirectoryScope(cwd);
            await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.MostRecent());
            var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();

            Assert.Equal(secondId, handle.Info.Id);
            Assert.Contains(events, evt => evt is AgentMessageEvent { Text: "newest" });
            Assert.Equal(cwd, locator.LastFilter?.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
        }
    }

    [Fact]
    public async Task ResumeSessionAsync_WithMostRecentTarget_UsesCurrentWorkingDirectoryFilter()
    {
        var baseTime = DateTimeOffset.Parse("2025-11-21T01:00:00Z");
        var insideId = SessionId.Parse("session-inside");
        var outsideId = SessionId.Parse("session-outside");
        var insidePath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime.AddMinutes(1), insideId);
        var cwd = Path.Combine(Path.GetTempPath(), $"codex-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cwd);

        try
        {
            var locator = new StubSessionLocator(
                insidePath,
                listedSessions:
                [
                    new CodexSessionInfo(outsideId, "C:\\sessions\\outside.jsonl", baseTime.AddMinutes(2), "D:\\other", null, "outside"),
                    new CodexSessionInfo(insideId, insidePath, baseTime.AddMinutes(1), cwd, null, "inside")
                ]);

            var logs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [insidePath] = new[]
                {
                    TestJsonlGenerator.GenerateSessionMeta(insideId, cwd, baseTime.AddMinutes(1)),
                    TestJsonlGenerator.GenerateAgentMessage("inside", baseTime.AddMinutes(1).AddSeconds(1))
                }
            };

            var client = new CodexClient(
                Options.Create(new CodexClientOptions()),
                processLauncher: null,
                locator,
                new PathAwareTailer(logs),
                new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>()),
                new FakePathProvider("C:\\sessions"),
                LoggerFactory.CreateLogger<CodexClient>(),
                LoggerFactory);

            using var scope = new CurrentDirectoryScope(cwd);
            await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.MostRecent());

            Assert.Equal(insideId, handle.Info.Id);
            Assert.Equal(cwd, locator.LastFilter?.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
        }
    }

    [Fact]
    public async Task ResumeSessionAsync_WithMostRecentTarget_UsesConfiguredModelProviderScope()
    {
        var baseTime = DateTimeOffset.Parse("2025-11-21T02:00:00Z");
        var openAiId = SessionId.Parse("session-openai");
        var localId = SessionId.Parse("session-local");
        var cwd = Path.Combine(Path.GetTempPath(), $"codex-provider-cwd-{Guid.NewGuid():N}");
        var codexHome = Path.Combine(Path.GetTempPath(), $"codex-provider-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cwd);
        Directory.CreateDirectory(codexHome);
        File.WriteAllText(Path.Combine(codexHome, "config.toml"), "model_provider = \"openai\"" + Environment.NewLine);

        try
        {
            var openAiPath = SessionLogPathTestHelper.BuildNestedRolloutPath("C:\\sessions", baseTime.AddMinutes(1), openAiId);
            var locator = new StubSessionLocator(
                openAiPath,
                listedSessions:
                [
                    new CodexSessionInfo(localId, "C:\\sessions\\local.jsonl", baseTime.AddMinutes(2), cwd, null, "local", ModelProvider: "local"),
                    new CodexSessionInfo(openAiId, openAiPath, baseTime.AddMinutes(1), cwd, null, "openai", ModelProvider: "openai")
                ]);

            var logs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [openAiPath] = new[]
                {
                    TestJsonlGenerator.GenerateSessionMeta(openAiId, cwd, baseTime.AddMinutes(1)),
                    TestJsonlGenerator.GenerateAgentMessage("openai", baseTime.AddMinutes(1).AddSeconds(1))
                }
            };

            var client = new CodexClient(
                Options.Create(new CodexClientOptions { CodexHomeDirectory = codexHome }),
                processLauncher: null,
                locator,
                new PathAwareTailer(logs),
                new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>()),
                new FakePathProvider("C:\\sessions"),
                LoggerFactory.CreateLogger<CodexClient>(),
                LoggerFactory);

            using var scope = new CurrentDirectoryScope(cwd);
            await using var handle = await client.ResumeSessionAsync(CodexResumeTarget.MostRecent());

            Assert.Equal(openAiId, handle.Info.Id);
            Assert.Equal("openai", locator.LastFilter?.ModelProvider);
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
            Directory.Delete(codexHome, recursive: true);
        }
    }

    private sealed class StubSessionLocator : ICodexSessionLocator
    {
        private readonly string _path;
        private readonly bool _throwOnFind;
        private readonly IReadOnlyList<CodexSessionInfo> _listedSessions;

        public StubSessionLocator(string path, bool throwOnFind = false, IReadOnlyList<CodexSessionInfo>? listedSessions = null)
        {
            _path = path;
            _throwOnFind = throwOnFind;
            _listedSessions = listedSessions ?? Array.Empty<CodexSessionInfo>();
        }

        public SessionFilter? LastFilter { get; private set; }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken)
        {
            if (_throwOnFind)
            {
                throw new FileNotFoundException("Session not found", sessionId.Value);
            }

            return Task.FromResult(_path);
        }

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new FileNotFoundException("Invalid path", logFilePath);
            }

            return Task.FromResult(logFilePath);
        }

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_throwOnFind)
            {
                throw new FileNotFoundException("Session not found", sessionId.Value);
            }
            return Task.FromResult(_path);
        }

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string sessionsRoot, SessionFilter? filter, CancellationToken cancellationToken)
        {
            return EnumerateAsync(filter, cancellationToken);
        }

        private async IAsyncEnumerable<CodexSessionInfo> EnumerateAsync(SessionFilter? filter, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastFilter = filter;

            foreach (var session in _listedSessions.OrderByDescending(session => session.CreatedAt))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (filter is not null &&
                    !string.IsNullOrWhiteSpace(filter.WorkingDirectory) &&
                    !string.Equals(filter.WorkingDirectory, session.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filter is not null &&
                    !string.IsNullOrWhiteSpace(filter.ModelProvider) &&
                    !string.Equals(filter.ModelProvider, session.ModelProvider, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return session;
                await Task.Yield();
            }
        }
    }

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _originalDirectory = Directory.GetCurrentDirectory();

        public CurrentDirectoryScope(string currentDirectory)
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
    }

    private sealed class FakeTailer : IJsonlTailer
    {
        private readonly IReadOnlyList<string> _lines;

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

    private sealed class PathAwareTailer(IReadOnlyDictionary<string, IReadOnlyList<string>> linesByPath) : IJsonlTailer
    {
        public async IAsyncEnumerable<string> TailAsync(string filePath, EventStreamOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (!linesByPath.TryGetValue(filePath, out var lines))
            {
                yield break;
            }

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
        }
    }
}
