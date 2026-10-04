using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SessionMetadataGapCoverageTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static CodexSessionInfo Session => new(SessionId.Parse("session-abc"), "/session.jsonl", Created,
        WorkingDirectory: Path.GetTempPath(), Model: CodexModel.Gpt51CodexMini, ModelProvider: "openai");

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("directory")]
    [InlineData("missing-model")]
    [InlineData("other-model")]
    [InlineData("pattern")]
    [InlineData("provider")]
    public void SessionFiltersRejectEachMismatchedCriterion(string criterion)
    {
        var session = criterion == "missing-model" ? Session with { Model = null } : Session;
        var filter = criterion switch
        {
            "before" => new SessionFilter(FromDate: Created.AddTicks(1)),
            "after" => new SessionFilter(ToDate: Created.AddTicks(-1)),
            "directory" => new SessionFilter(WorkingDirectory: Path.Combine(Path.GetTempPath(), "other")),
            "missing-model" => new SessionFilter(Model: CodexModel.Gpt51CodexMini),
            "other-model" => new SessionFilter(Model: CodexModel.Parse("different-model")),
            "pattern" => new SessionFilter(SessionIdPattern: "other-*"),
            _ => new SessionFilter(ModelProvider: "OpenAI")
        };
        CodexSessionLocatorHelpers.MatchesFilter(session, filter).Should().BeFalse();
        CodexSessionLocatorHelpers.MatchesFilter(Session, new(FromDate: Created, ToDate: Created,
            WorkingDirectory: Path.GetTempPath(), Model: CodexModel.Parse("GPT-5.1-CODEX-MINI"), SessionIdPattern: "session-?b*", ModelProvider: "openai")).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc.def", "abc.def", true)]
    [InlineData("abcZdef", "abc.def", false)]
    [InlineData("[abc]", "[abc]", true)]
    [InlineData("abc", "[abc]", false)]
    [InlineData("SESSION-abc", "session-?b*", true)]
    [InlineData("prefix-session-abc", "session-*", false)]
    public void WildcardsAreAnchoredAndRegexMetacharactersAreLiteral(string value, string pattern, bool expected) =>
        CodexSessionLocatorHelpers.MatchesPattern(value, pattern).Should().Be(expected);

    [Theory]
    [InlineData("", null)]
    [InlineData("unknown.jsonl", null)]
    [InlineData("rollout-.jsonl", null)]
    [InlineData("arbitrary-12345678-1234-1234-1234-123456789abc.jsonl", "12345678-1234-1234-1234-123456789abc")]
    [InlineData("rollout-custom-selector.jsonl", "custom-selector")]
    public void FilenameFallbackAcceptsSelectorsAndRejectsEmptyNames(string path, string? expected) =>
        (CodexSessionLocatorHelpers.TryExtractSessionIdFromFilePath(NullLogger.Instance, path)?.Value).Should().Be(expected);

    [Fact]
    public async Task MetadataParserSkipsMalformedLinesAndRejectsUnidentifiableFiles()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("unknown.jsonl", "bad json\n\n{\"type\":\"other\"}\n");
        (await CodexSessionLocatorHelpers.ParseSessionInfoAsync(fs, NullLogger.Instance, "unknown.jsonl", null, default)).Should().BeNull();
        fs.AddFile("rollout-fallback.jsonl", "bad json\n");
        var before = DateTimeOffset.UtcNow;
        var parsed = await CodexSessionLocatorHelpers.ParseSessionInfoAsync(fs, NullLogger.Instance, "rollout-fallback.jsonl", null, default);
        parsed!.Id.Value.Should().Be("fallback");
        parsed.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void MetadataEnumerationSkipsNonSessionFilesAndUnavailableCreationTimes()
    {
        var files = new MetadataFiles();
        var entries = CodexSessionLocatorHelpers.EnumerateSessionFiles(files, NullLogger.Instance, "/sessions", CodexSessionFilePattern.Create()).ToArray();
        entries.Select(x => x.FilePath).Should().Equal("rollout-2026-99-01T00-00-00-b.jsonl", "rollout-2026-99-01T00-00-00-a.jsonl");
        entries.Should().OnlyContain(x => x.CreatedAtUtc == null);
        files.FailEnumeration = true;
        CodexSessionLocatorHelpers.EnumerateSessionFiles(files, NullLogger.Instance, "/sessions", CodexSessionFilePattern.Create()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UnavailableCreationTimesAreOptional(int kind)
    {
        var files = new MetadataFiles { Failure = kind switch { 0 => new FileNotFoundException(), 1 => new UnauthorizedAccessException(), _ => new IOException() } };
        CodexSessionLocatorHelpers.TryGetCreationTimeUtc(files, NullLogger.Instance, "file").Should().BeNull();
    }

    private sealed class MetadataFiles : IFileSystem
    {
        public bool FailEnumeration { get; set; }
        public Exception Failure { get; init; } = new IOException();
        public bool FileExists(string path) => true;
        public bool DirectoryExists(string path) => true;
        public IEnumerable<string> GetFiles(string directory, string searchPattern) => FailEnumeration ? throw new IOException() : ["", "notes.jsonl", "rollout-2026-99-01T00-00-00-a.jsonl", "rollout-2026-99-01T00-00-00-b.jsonl"];
        public Stream OpenRead(string path) => throw new NotSupportedException();
        public DateTime GetFileCreationTimeUtc(string path) => throw Failure;
        public long GetFileSize(string path) => 0;
    }
}
