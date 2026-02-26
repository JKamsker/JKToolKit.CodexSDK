using FluentAssertions;
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
}

