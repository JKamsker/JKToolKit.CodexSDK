using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class InfrastructureDiagnosticCoverageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void EmptyDiagnosticIsEmpty(string? input) => CodexDiagnosticsSanitizer.Sanitize(input, 10).Should().BeEmpty();

    [Theory]
    [InlineData("abcdef", 3, "abc")]
    [InlineData("abcdef", 0, "abcdef")]
    [InlineData("abcdef", -1, "abcdef")]
    [InlineData("abc   ", 3, "abc")]
    [InlineData("abc[REDACTED]tail", 5, "abc[REDACTED]")]
    [InlineData("abc[REDACTED]tail", 12, "abc[REDACTED]")]
    [InlineData("abc[x]tail", 7, "abc[x]t")]
    [InlineData("abc[unfinished", 5, "abc")]
    [InlineData("[unfinished", 5, "")]
    public void TruncationPreservesWholeRedactionMarkers(string input, int limit, string expected) =>
        CodexDiagnosticsSanitizer.Sanitize(input, limit).Should().Be(expected);

    [Fact]
    public void OverlongMarkerIsRemovedAtOpeningBracket()
    {
        CodexDiagnosticsSanitizer.Sanitize("prefix[" + new string('x', 65) + "]tail", 10).Should().Be("prefix");
        CodexDiagnosticsSanitizer.Sanitize("[" + new string('x', 64) + "]tail", 10).Should().BeEmpty();
        CodexDiagnosticsSanitizer.Sanitize("[" + new string('x', 63) + "]tail", 10).Should().Be("[" + new string('x', 63) + "]");
    }

    [Fact]
    public void SecretsAreRedactedBeforeTruncation()
    {
        var input = "Authorization: Bearer secret api_key=key sk-abcdefghijklmnopqrst ghp_abcdefghijklmnopqrst AKIA1234567890123456 a@example.com";
        var clean = CodexDiagnosticsSanitizer.Sanitize(input, 0);
        clean.Should().Be("Authorization: Bearer [REDACTED] api_key=[REDACTED] sk-[REDACTED] [REDACTED_TOKEN] AKIA[REDACTED] [REDACTED_EMAIL]");
        CodexDiagnosticsSanitizer.Sanitize("token=secret", 8).Should().Be("token=[REDACTED]");
    }

    [Fact]
    public void LineSanitizerKeepsLastNonblankLinesAndAppliesLimits()
    {
        CodexDiagnosticsSanitizer.SanitizeLines(null!, 2, 4).Should().BeEmpty();
        CodexDiagnosticsSanitizer.SanitizeLines(["line"], 0, 4).Should().BeEmpty();
        CodexDiagnosticsSanitizer.SanitizeLines(["line"], -1, 4).Should().BeEmpty();
        CodexDiagnosticsSanitizer.SanitizeLines(["first", "", "second", " ", "third-long"], 2, 5).Should().Equal("secon", "third");
    }
}
