using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SessionLocatorBoundaryCoverageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListSessions_PreservesCancellationWhenEmptyOrDuringMetadataRead(bool duringRead)
    {
        using var cts = new CancellationTokenSource();
        var fs = new Files { FilesToReturn = duringRead ? ["/sessions/rollout-test.jsonl"] : [] };
        if (duringRead) fs.Read = _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); };
        else cts.Cancel();
        var locator = Locator(fs);
        await using var reader = locator.ListSessionsAsync("/sessions", null, cts.Token).GetAsyncEnumerator();
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ListSessions_SkipsUnreadableFilesAndRetainsFilenameFallback()
    {
        var fs = new Files { FilesToReturn = ["/sessions/rollout-unreadable.jsonl", "/sessions/rollout-empty.jsonl"] };
        fs.Read = path => path.Contains("unreadable") ? throw new IOException("unreadable") : new MemoryStream();
        await using var reader = Locator(fs).ListSessionsAsync("/sessions", null, default).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeTrue();
        reader.Current.LogPath.Should().Be("/sessions/rollout-empty.jsonl");
        (await reader.MoveNextAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ValidateLog_PreservesIoErrors(int errorKind)
    {
        Exception error = errorKind switch { 0 => new FileNotFoundException("missing"), 1 => new UnauthorizedAccessException("denied"), _ => new IOException("io") };
        var fs = new Files { Read = _ => throw error };
        var ex = await Record.ExceptionAsync(() => Locator(fs).ValidateLogFileAsync("/sessions/log", default));
        ex.Should().BeSameAs(error);
    }

    [Fact]
    public async Task Locator_RejectsMissingRootAndInvalidTimeoutOrPath()
    {
        var fs = new Files { RootExists = false };
        var locator = Locator(fs);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => locator.WaitForNewSessionFileAsync("/sessions", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), default));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => locator.FindSessionLogAsync(SessionId.Parse("id"), "/sessions", default));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => locator.WaitForSessionLogByIdAsync(SessionId.Parse("id"), "/sessions", TimeSpan.FromSeconds(1), default));
        fs.RootExists = true;
        await Assert.ThrowsAsync<ArgumentException>(() => locator.WaitForNewSessionFileAsync("/sessions", DateTimeOffset.UtcNow, TimeSpan.Zero, default));
        await Assert.ThrowsAsync<ArgumentException>(() => locator.ValidateLogFileAsync(" ", default));
        fs.FilePresent = false;
        await Assert.ThrowsAsync<FileNotFoundException>(() => locator.ValidateLogFileAsync("/sessions/log", default));
    }

    [Fact]
    public async Task WaitForMissingSession_TimesOutWithSessionIdentity()
    {
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => Locator(new Files()).WaitForSessionLogByIdAsync(
            SessionId.Parse("missing-id"), "/sessions", TimeSpan.FromMilliseconds(50), default));
        ex.Message.Should().Contain("missing-id");
    }

    private static CodexSessionLocator Locator(Files fs) => new(fs, NullLogger<CodexSessionLocator>.Instance);
    private sealed class Files : IFileSystem
    {
        public bool RootExists { get; set; } = true;
        public bool FilePresent { get; set; } = true;
        public string[] FilesToReturn { get; set; } = [];
        public Func<string, Stream> Read { get; set; } = _ => new MemoryStream();
        public bool FileExists(string path) => FilePresent;
        public bool DirectoryExists(string path) => RootExists;
        public IEnumerable<string> GetFiles(string directory, string searchPattern) => FilesToReturn;
        public Stream OpenRead(string path) => Read(path);
        public DateTime GetFileCreationTimeUtc(string path) => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public long GetFileSize(string path) => 0;
    }
}
