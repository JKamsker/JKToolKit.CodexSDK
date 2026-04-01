using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSessionLocatorFindByIdTests
{
    [Fact]
    public async Task FindSessionLogAsync_TreatsSessionIdAsLiteral_WhenItContainsWildcardCharacters()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var sessionId = SessionId.Parse("a*c");

        var literalMatch = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-00-a*c.jsonl");
        var nonMatch = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-00-abc.jsonl");

        var fileSystem = new RecordingFileSystem(
            sessionsRoot,
            files: new[] { nonMatch, literalMatch });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var resolved = await locator.FindSessionLogAsync(sessionId, sessionsRoot, CancellationToken.None);

        resolved.Should().Be(literalMatch);
        fileSystem.LastSearchPattern.Should().Be("*.jsonl");
    }

    [Fact]
    public async Task FindSessionLogAsync_SelectsNewestTimestampedRollout_WhenMultipleMatchesExist()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var sessionId = SessionId.Parse("id");

        var generic = Path.Combine(sessionsRoot, "foo-id.jsonl");
        var legacy = Path.Combine(sessionsRoot, "rollout-id.jsonl");
        var older = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T12-00-00-id.jsonl");
        var newer = Path.Combine(sessionsRoot, "2026", "02", "26", "rollout-2026-02-26T13-00-00-id.jsonl");

        var fileSystem = new RecordingFileSystem(
            sessionsRoot,
            files: new[] { legacy, older, generic, newer });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var resolved = await locator.FindSessionLogAsync(sessionId, sessionsRoot, CancellationToken.None);

        resolved.Should().Be(newer);
    }

    [Fact]
    public async Task FindSessionLogAsync_PrefersLegacyRolloutName_OverGenericSuffixMatch_WhenNoTimestampedRolloutExists()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var sessionId = SessionId.Parse("id");

        var legacy = Path.Combine(sessionsRoot, "rollout-id.jsonl");
        var generic = Path.Combine(sessionsRoot, "anything-id.jsonl");

        var fileSystem = new RecordingFileSystem(
            sessionsRoot,
            files: new[] { generic, legacy });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var resolved = await locator.FindSessionLogAsync(sessionId, sessionsRoot, CancellationToken.None);

        resolved.Should().Be(legacy);
    }

    [Fact]
    public async Task FindSessionLogAsync_UsesDeterministicPathOrder_WhenTimestampedCandidatesTie()
    {
        var sessionsRoot = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var sessionId = SessionId.Parse("id");

        var candidateA = Path.Combine(sessionsRoot, "a", "rollout-2026-02-26T12-00-00-id.jsonl");
        var candidateB = Path.Combine(sessionsRoot, "b", "rollout-2026-02-26T12-00-00-id.jsonl");

        var fileSystem = new RecordingFileSystem(
            sessionsRoot,
            files: new[] { candidateB, candidateA });

        var locator = new CodexSessionLocator(fileSystem, NullLogger<CodexSessionLocator>.Instance);

        var resolved = await locator.FindSessionLogAsync(sessionId, sessionsRoot, CancellationToken.None);

        resolved.Should().Be(candidateA);
    }

    private sealed class RecordingFileSystem(string sessionsRoot, IReadOnlyList<string> files) : IFileSystem
    {
        public string? LastSearchPattern { get; private set; }

        public bool FileExists(string path) => files.Contains(path, StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) =>
            string.Equals(path, sessionsRoot, StringComparison.OrdinalIgnoreCase);

        public IEnumerable<string> GetFiles(string directory, string searchPattern)
        {
            LastSearchPattern = searchPattern;
            return string.Equals(directory, sessionsRoot, StringComparison.OrdinalIgnoreCase)
                ? files
                : Array.Empty<string>();
        }

        public Stream OpenRead(string path) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));

        public DateTime GetFileCreationTimeUtc(string path) => DateTime.UtcNow;

        public long GetFileSize(string path) => 2;
    }
}
