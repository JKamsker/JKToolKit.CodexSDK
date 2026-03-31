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
}

