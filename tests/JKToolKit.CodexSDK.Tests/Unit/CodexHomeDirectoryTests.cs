using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexHomeDirectoryTests
{
    [Fact]
    public async Task ListSessionsAsync_UsesCodexHomeDirectorySessionsFolder_WhenSessionsRootNotSet()
    {
        var codexHome = Path.Combine(Path.GetTempPath(), $"codex-home-{Guid.NewGuid():N}");
        try
        {
            var clientOptions = new CodexClientOptions
            {
                CodexHomeDirectory = codexHome,
                SessionsRootDirectory = null
            };

            var pathProvider = new RecordingSessionsRootPathProvider();
            var sessionLocator = new RecordingListSessionsLocator();

            using var client = new CodexClient(
                Options.Create(clientOptions),
                processLauncher: null,
                sessionLocator: sessionLocator,
                tailer: null,
                parser: null,
                pathProvider: pathProvider,
                logger: NullLogger<CodexClient>.Instance,
                loggerFactory: NullLoggerFactory.Instance);

            _ = await client.ListSessionsAsync().ToListAsync();

            var expected = Path.Combine(codexHome, "sessions");
            pathProvider.LastOverrideDirectory.Should().Be(expected);
            sessionLocator.LastSessionsRoot.Should().Be(expected);
            Directory.Exists(expected).Should().BeTrue();
        }
        finally
        {
            try { if (Directory.Exists(codexHome)) Directory.Delete(codexHome, recursive: true); } catch { }
        }
    }

    private sealed class RecordingSessionsRootPathProvider : ICodexPathProvider
    {
        public string? LastOverrideDirectory { get; private set; }

        public string GetCodexExecutablePath(string? overridePath) =>
            throw new NotSupportedException();

        public string GetSessionsRootDirectory(string? overrideDirectory)
        {
            LastOverrideDirectory = overrideDirectory;
            return overrideDirectory ?? throw new InvalidOperationException("Expected overrideDirectory to be set.");
        }

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingListSessionsLocator : ICodexSessionLocator
    {
        public string? LastSessionsRoot { get; private set; }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string sessionsRoot, SessionFilter? filter, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastSessionsRoot = sessionsRoot;
            await Task.CompletedTask;
            yield break;
        }
    }
}
