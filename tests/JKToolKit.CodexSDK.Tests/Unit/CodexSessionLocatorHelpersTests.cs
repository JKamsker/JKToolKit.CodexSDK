using FluentAssertions;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSessionLocatorHelpersTests
{
    [Fact]
    public void TryExtractSessionIdFromFilePath_ExtractsFullHyphenatedUuid_FromTimestampedRolloutName()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            "sessions",
            "2026",
            "02",
            "26",
            "rollout-2026-02-26T12-00-00-11111111-1111-1111-1111-111111111111.jsonl");

        var extracted = CodexSessionLocatorHelpers.TryExtractSessionIdFromFilePath(NullLogger.Instance, filePath);

        extracted.Should().NotBeNull();
        extracted!.Value.Value.Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public void TryExtractSessionIdFromFilePath_ExtractsFullHyphenatedUuid_FromRolloutNameWithoutTimestamp()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            "sessions",
            "rollout-22222222-2222-2222-2222-222222222222.jsonl");

        var extracted = CodexSessionLocatorHelpers.TryExtractSessionIdFromFilePath(NullLogger.Instance, filePath);

        extracted.Should().NotBeNull();
        extracted!.Value.Value.Should().Be("22222222-2222-2222-2222-222222222222");
    }

    [Fact]
    public async Task ParseSessionInfoAsync_CapturesHumanLabel_FromSessionMetaName()
    {
        var fileSystem = new InMemoryFileSystem();
        var filePath = Path.Combine(Path.GetTempPath(), "sessions", "named.jsonl");
        var sessionJson =
            """{"type":"session_meta","timestamp":"2026-04-01T10:00:00Z","payload":{"id":"named-session","cwd":"C:\\repo","name":"Friendly Thread"}}""";
        fileSystem.AddFile(filePath, sessionJson);

        var session = await CodexSessionLocatorHelpers.ParseSessionInfoAsync(
            fileSystem,
            NullLogger.Instance,
            filePath,
            creationTimeUtc: null,
            CancellationToken.None);

        session.Should().NotBeNull();
        session!.HumanLabel.Should().Be("Friendly Thread");
    }

    [Fact]
    public async Task ParseSessionInfoAsync_PrefersLatestTurnContext_ForCwdAndModel()
    {
        var fileSystem = new InMemoryFileSystem();
        var filePath = Path.Combine(Path.GetTempPath(), "sessions", "turn-context.jsonl");
        fileSystem.AddFile(
            filePath,
            string.Join(
                Environment.NewLine,
                """{"type":"session_meta","timestamp":"2026-04-01T10:00:00Z","payload":{"id":"turn-context-session","timestamp":"2026-04-01T09:59:59Z","cwd":"C:\\stale","name":"Thread"}}""",
                """{"type":"turn_context","timestamp":"2026-04-01T10:01:00Z","payload":{"cwd":"C:\\latest","model":"gpt-5"}}""",
                """{"type":"turn_context","timestamp":"2026-04-01T10:02:00Z","payload":{"cwd":"D:\\latest-2","model":"gpt-5-codex"}}"""
            ));

        var session = await CodexSessionLocatorHelpers.ParseSessionInfoAsync(
            fileSystem,
            NullLogger.Instance,
            filePath,
            creationTimeUtc: null,
            CancellationToken.None);

        session.Should().NotBeNull();
        session!.WorkingDirectory.Should().Be("D:\\latest-2");
        session.Model.Should().Be(JKToolKit.CodexSDK.Models.CodexModel.Parse("gpt-5-codex"));
        session.CreatedAt.Should().Be(DateTimeOffset.Parse("2026-04-01T10:00:00Z"));
        session.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-04-01T10:02:00Z"));
    }

    [Fact]
    public async Task ParseSessionInfoAsync_DoesNotInferModel_FromSessionMetaOnly()
    {
        var fileSystem = new InMemoryFileSystem();
        var filePath = Path.Combine(Path.GetTempPath(), "sessions", "session-meta-only.jsonl");
        fileSystem.AddFile(
            filePath,
            """{"type":"session_meta","payload":{"id":"session-meta-only","timestamp":"2026-04-01T09:59:59Z","cwd":"C:\\repo","model":"gpt-5"}}""");

        var session = await CodexSessionLocatorHelpers.ParseSessionInfoAsync(
            fileSystem,
            NullLogger.Instance,
            filePath,
            creationTimeUtc: null,
            CancellationToken.None);

        session.Should().NotBeNull();
        session!.Model.Should().BeNull();
        session.CreatedAt.Should().Be(DateTimeOffset.Parse("2026-04-01T09:59:59Z"));
        session.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-04-01T09:59:59Z"));
    }

    [Theory]
    [InlineData(@"C:\repo", @"C:\repo", true)]
    [InlineData(@"C:\repo\", @"C:\repo", true)]
    [InlineData(@"C:\repo", @"C:\repo\", true)]
    [InlineData(@"C:\repo\sub\..", @"C:\repo", true)]
    [InlineData(@"C:\repo", @"D:\repo", false)]
    [InlineData(null, null, true)]
    [InlineData(@"C:\repo", null, false)]
    [InlineData(null, @"C:\repo", false)]
    public void NormalizedPathEquals_HandlesVariousPathForms(string? a, string? b, bool expected)
    {
        CodexSessionLocatorHelpers.NormalizedPathEquals(a, b).Should().Be(expected);
    }
}

