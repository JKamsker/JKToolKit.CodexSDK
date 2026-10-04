using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SessionDiscoveryGapCoverageTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "discovery-gap");
    private static string FilePath(string name) => Path.Combine(Root, name);

    [Fact]
    public void SnapshotSkipsUnavailableRootsAndRetainsEntriesBeforeEnumerationFailure()
    {
        var fs = new Files { Exists = root => root != "missing", Enumerate = root => root == "denied" ? throw new UnauthorizedAccessException() : ThrowAfterOne() };
        var snapshot = CodexUncorrelatedSessionDiscoveryHelpers.CaptureSessionSnapshot(fs, NullLogger.Instance,
            ["", "missing", "denied", Root], CodexSessionFilePattern.Create());
        snapshot.Should().Equal(FilePath("rollout-known.jsonl"));
        static IEnumerable<string> ThrowAfterOne()
        {
            yield return FilePath("notes.jsonl");
            yield return FilePath("rollout-known.jsonl");
            throw new IOException("directory changed");
        }
    }

    [Fact]
    public async Task DiscoveryUsesMetadataTimestampAndSortsEarliestFirst()
    {
        var late = FilePath("rollout-a-late.jsonl");
        var early = FilePath("rollout-z-early.jsonl");
        var old = FilePath("rollout-old.jsonl");
        var unknown = FilePath("rollout-unknown.jsonl");
        var fs = new Files
        {
            Enumerate = _ => [late, early, old, unknown, early],
            Read = path => Text(path == early ? "\n{\"type\":\"other\"}\n{\"type\":\"session_meta\",\"payload\":{\"timestamp\":\"2026-01-01T00:00:01Z\"}}"
                : path == late ? "{\"type\":\"session_meta\",\"timestamp\":\"2026-01-01T00:00:02Z\",\"payload\":{}}"
                : path == old ? "{\"type\":\"session_meta\",\"timestamp\":\"2025-12-31T23:59:59Z\",\"payload\":{}}" : "")
        };
        (await FindAsync(fs, [])).Should().Be(early);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"other\"}")]
    [InlineData("{\"type\":\"session_meta\",\"payload\":{}}")]
    public async Task UnknownTimestampCannotReattachBaselineSession(string content)
    {
        var path = FilePath("rollout-existing.jsonl");
        var fs = new Files { Enumerate = _ => [path], Read = _ => Text(content) };
        (await FindAsync(fs, [path])).Should().BeNull();
        (await FindAsync(fs, [])).Should().Be(path);
    }

    [Fact]
    public async Task DiscoveryContinuesPastUnreadableRootAndMetadata()
    {
        var path = FilePath("rollout-unknown.jsonl");
        var fs = new Files { Exists = root => root != "missing", Enumerate = root => root == "denied" ? throw new IOException() : [path], Read = _ => throw new IOException() };
        (await CodexUncorrelatedSessionDiscoveryHelpers.FindNewSessionFileAsync(fs, NullLogger.Instance,
            ["", "missing", "denied", Root], Start, [], CodexSessionFilePattern.Create(), default)).Should().Be(path);
    }

    [Fact]
    public async Task DiscoveryTreatsUnexpectedFilesystemFailuresAsBestEffort()
    {
        var fs = new Files { Exists = _ => throw new IOException("root vanished") };
        (await FindAsync(fs, [])).Should().BeNull();
        var invalidRoot = "\0";
        fs = new Files { Enumerate = _ => [FilePath("rollout-test.jsonl")] };
        (await CodexUncorrelatedSessionDiscoveryHelpers.FindNewSessionFileAsync(fs, NullLogger.Instance,
            [invalidRoot], Start, [], CodexSessionFilePattern.Create(), default)).Should().BeNull();
    }

    [Fact]
    public async Task DiscoveryPropagatesCancellationFromMetadataRead()
    {
        using var cts = new CancellationTokenSource();
        var fs = new Files { Enumerate = _ => [FilePath("rollout-test.jsonl")], Read = _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); } };
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FindAsync(fs, [], cts.Token));
        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rollout-2026-99-01T00-00-00-id.jsonl")]
    [InlineData("rollout-2026-01-01t00-00-00-id.jsonl")]
    [InlineData("other.jsonl")]
    public void InvalidFilenameTimestampIsRejected(string path) =>
        CodexUncorrelatedSessionDiscoveryHelpers.TryParseRolloutTimestampUtc(path, out _).Should().BeFalse();

    [Theory]
    [InlineData("rollout-2026-04-05T06-07-08-session.jsonl")]
    [InlineData("ROLLOUT-2026-04-05T06-07-08-session.jsonl")]
    public void FilenameTimestampUsesUtcComponents(string name)
    {
        CodexUncorrelatedSessionDiscoveryHelpers.TryParseRolloutTimestampUtc(FilePath(name), out var timestamp).Should().BeTrue();
        timestamp.Should().Be(new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero));
    }

    [Fact]
    public async Task DiscoveryPrefersTopLevelMetadataTimestampAndIgnoresUnrelatedEvents()
    {
        var top = FilePath("rollout-z-top.jsonl");
        var other = FilePath("rollout-a-other.jsonl");
        var fs = new Files
        {
            Enumerate = _ => [other, top],
            Read = path => Text(path == top
                ? "\n{\"type\":\"other\",\"timestamp\":\"2025-01-01T00:00:00Z\"}\n{\"type\":\"session_meta\",\"timestamp\":\"2026-01-01T00:00:01Z\",\"payload\":{\"timestamp\":\"2026-01-01T00:00:03Z\"}}"
                : "{\"type\":\"session_meta\",\"timestamp\":\"2026-01-01T00:00:02Z\",\"payload\":{}}")
        };
        (await FindAsync(fs, [])).Should().Be(top);
    }

    private static Task<string?> FindAsync(Files fs, HashSet<string> baseline, CancellationToken ct = default) =>
        CodexUncorrelatedSessionDiscoveryHelpers.FindNewSessionFileAsync(fs, NullLogger.Instance, [Root], Start, baseline, CodexSessionFilePattern.Create(), ct);
    private static Stream Text(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    private sealed class Files : IFileSystem
    {
        public Func<string, bool> Exists { get; init; } = _ => true;
        public Func<string, IEnumerable<string>> Enumerate { get; init; } = _ => [];
        public Func<string, Stream> Read { get; init; } = _ => Text("");
        public bool FileExists(string path) => true;
        public bool DirectoryExists(string path) => Exists(path);
        public IEnumerable<string> GetFiles(string directory, string searchPattern) => Enumerate(directory);
        public Stream OpenRead(string path) => Read(path);
        public DateTime GetFileCreationTimeUtc(string path) => Start.UtcDateTime;
        public long GetFileSize(string path) => 0;
    }
}
