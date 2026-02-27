using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexUncorrelatedSessionDiscoveryTests
{
    [Fact]
    public async Task WaitForNewSessionFileAsync_DoesNotMissFileThatAppearsBetweenStartTimeAndBaselineSnapshot()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var newFile = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-01-new.jsonl");
        var oldFile = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T11-59-59-old.jsonl");

        var fileSystem = new FakeFileSystem(
            directoriesThatExist: new[] { sessionsRoot },
            filesByDirectory: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [sessionsRoot] = new[] { oldFile, newFile }
            });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var startTime = new DateTimeOffset(2026, 02, 26, 12, 00, 00, TimeSpan.Zero);
        var path = await locator.WaitForNewSessionFileAsync(
            sessionsRoot,
            startTime,
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        path.Should().Be(newFile);
    }

    [Fact]
    public async Task WaitForNewSessionFileAsync_PicksEarliestCandidateByFilenameTimestamp()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var first = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-01-a.jsonl");
        var second = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-02-b.jsonl");

        var fileSystem = new FakeFileSystem(
            directoriesThatExist: new[] { sessionsRoot },
            filesByDirectory: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [sessionsRoot] = new[] { second, first }
            });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var startTime = new DateTimeOffset(2026, 02, 26, 12, 00, 00, TimeSpan.Zero);
        var path = await locator.WaitForNewSessionFileAsync(
            sessionsRoot,
            startTime,
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        path.Should().Be(first);
    }

    [Fact]
    public async Task WaitForNewSessionFileAsync_IgnoresFileOutsideSessionsRoot_EvenIfReturnedByFileSystem()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var outsideRoot = Path.Combine(Path.GetTempPath(), $"other-{Guid.NewGuid():N}");
        var outside = Path.Combine(outsideRoot, "rollout-2026-02-26T12-00-01-outside.jsonl");
        var inside = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-01-inside.jsonl");

        var fileSystem = new FakeFileSystem(
            directoriesThatExist: new[] { sessionsRoot },
            filesByDirectory: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [sessionsRoot] = new[] { outside, inside }
            });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var startTime = new DateTimeOffset(2026, 02, 26, 12, 00, 00, TimeSpan.Zero);
        var path = await locator.WaitForNewSessionFileAsync(
            sessionsRoot,
            startTime,
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        path.Should().Be(inside);
    }

    private sealed class FakeFileSystem(
        IReadOnlyCollection<string> directoriesThatExist,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filesByDirectory) : IFileSystem
    {
        private readonly HashSet<string> _directoriesThatExist = new(directoriesThatExist, StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _filesByDirectory = filesByDirectory;

        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => _directoriesThatExist.Contains(path);

        public IEnumerable<string> GetFiles(string directory, string searchPattern) =>
            _filesByDirectory.TryGetValue(directory, out var files) ? files : Array.Empty<string>();

        public Stream OpenRead(string path) => throw new FileNotFoundException();

        public DateTime GetFileCreationTimeUtc(string path) => throw new FileNotFoundException();

        public long GetFileSize(string path) => throw new FileNotFoundException();
    }
}
