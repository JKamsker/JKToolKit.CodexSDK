using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSessionLocatorCancellationTests
{
    [Fact]
    public async Task WaitForNewSessionFileAsync_ThrowsOperationCanceledException_WhenCallerTokenAlreadyCanceled()
    {
        var fileSystem = new EmptyFileSystem();
        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
            await locator.WaitForNewSessionFileAsync(
                sessionsRoot: "C:\\sessions",
                startTime: DateTimeOffset.UtcNow,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForSessionLogByIdAsync_ThrowsOperationCanceledException_WhenCallerCancels()
    {
        var fileSystem = new EmptyFileSystem();
        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        var act = async () =>
            await locator.WaitForSessionLogByIdAsync(
                sessionId: SessionId.Parse("session-123"),
                sessionsRoot: "C:\\sessions",
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class EmptyFileSystem : IFileSystem
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => true;

        public IEnumerable<string> GetFiles(string directory, string searchPattern) => Array.Empty<string>();

        public Stream OpenRead(string path) => throw new FileNotFoundException();

        public DateTime GetFileCreationTimeUtc(string path) => throw new FileNotFoundException();

        public long GetFileSize(string path) => throw new FileNotFoundException();
    }
}

