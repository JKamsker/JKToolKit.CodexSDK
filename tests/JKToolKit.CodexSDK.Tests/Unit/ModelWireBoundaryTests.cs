using System.Text.Json;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.StructuredOutputs.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ModelWireBoundaryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("3")]
    [InlineData("[]")]
    [InlineData("\"bad\"")]
    [InlineData("{}")]
    [InlineData("{\"sandbox_approval\":true}")]
    [InlineData("{\"sandbox_approval\":true,\"rules\":false}")]
    [InlineData("{\"sandbox_approval\":true,\"rules\":false,\"mcp_elicitations\":3}")]
    public void ApprovalTryParse_MalformedGranularReturnsFalse(string granular)
    {
        using var doc = JsonDocument.Parse("{\"granular\":" + granular + "}");
        Assert.False(CodexAskForApproval.TryParse(doc.RootElement, out var value));
        Assert.Equal(default, value);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void LegacyRejectForm_InvertsOnlyItsThreeCategories(bool mcp, bool rules, bool sandbox)
    {
        CodexAskForApproval value = CodexAskForApproval.Rejecting(mcp, rules, sandbox);
        Assert.Equal(!mcp, value.Granular!.McpElicitations);
        Assert.Equal(!rules, value.Granular.Rules);
        Assert.Equal(!sandbox, value.Granular.SandboxApproval);
        Assert.True(value.Granular.SkillApproval);
        Assert.True(value.Granular.RequestPermissions);
        Assert.Equal(new CodexAskForApprovalReject { McpElicitations = mcp, Rules = rules, SandboxApproval = sandbox }, value.Reject);
        CodexAskForApproval converted = value.Reject!;
        Assert.Equal(value, converted);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value.ToWireValue()));
        Assert.True(CodexAskForApproval.TryParse(doc.RootElement, out var parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public void ApprovalConversions_PreserveFuturePolicyAndGranularValues()
    {
        CodexAskForApproval simple = CodexApprovalPolicy.OnRequest;
        Assert.Equal("on-request", simple.ToWireValue());
        CodexAskForApproval future = "future-policy";
        Assert.Equal("future-policy", future.ToWireValue());
        Assert.Null(future.Reject);
        var granular = new CodexAskForApprovalGranular { McpElicitations = true, Rules = false, SandboxApproval = true };
        CodexAskForApproval fromGranular = granular;
        Assert.Same(granular, fromGranular.Granular);
        Assert.Throws<InvalidOperationException>(() => default(CodexAskForApproval).ToWireValue());
        using var invalid = JsonDocument.Parse("\" \"");
        Assert.False(CodexAskForApproval.TryParse(invalid.RootElement, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void WireIdentifiers_RejectMissingValues(string? value)
    {
        Assert.Throws<ArgumentException>(() => CodexApprovalPolicy.Parse(value!));
        Assert.Throws<ArgumentException>(() => CodexSandboxMode.Parse(value!));
        Assert.Throws<ArgumentException>(() => CodexReasoningEffort.Parse(value!));
        Assert.Throws<ArgumentException>(() => CodexServiceTier.Parse(value!));
        Assert.Throws<ArgumentException>(() => CodexResidencyRequirement.Parse(value!));
        Assert.Throws<ArgumentException>(() => CodexWebSearchMode.Parse(value!));
        Assert.False(CodexWebSearchMode.TryParse(value, out var web));
        Assert.Equal(default, web);
        Assert.False(CodexResidencyRequirement.TryParse(value, out var residency));
        Assert.Equal(default, residency);
    }

    [Fact]
    public void WireIdentifiers_PreserveFutureValuesAndKnownConstants()
    {
        CodexApprovalPolicy approval = "future";
        CodexSandboxMode sandbox = "future";
        CodexServiceTier tier = "future";
        CodexResidencyRequirement residency = "future";
        CodexWebSearchMode web = "future";
        Assert.Equal("future", (string)approval);
        Assert.Equal("future", approval.ToString());
        Assert.Equal("future", approval.ToMcpWireValue());
        Assert.Equal("future", (string)sandbox);
        Assert.Equal("future", sandbox.ToString());
        Assert.Equal("future", sandbox.ToMcpWireValue());
        Assert.Equal("future", sandbox.ToAppServerWireValue());
        Assert.Equal("future", (string)tier);
        Assert.Equal("future", tier.ToString());
        Assert.Equal("future", (string)residency);
        Assert.Equal("future", residency.ToString());
        Assert.True(CodexResidencyRequirement.TryParse("future", out var parsedResidency));
        Assert.Equal(residency, parsedResidency);
        Assert.Equal("future", (string)web);
        Assert.Equal("future", web.ToString());
        Assert.True(CodexWebSearchMode.TryParse("future", out var parsedWeb));
        Assert.Equal(web, parsedWeb);
        Assert.Equal("", (string)default(CodexWebSearchMode));
        Assert.Equal("", default(CodexWebSearchMode).ToString());
        Assert.Equal("", (string)default(CodexResidencyRequirement));
        Assert.Equal("", default(CodexResidencyRequirement).ToString());
        Assert.Equal("none", CodexReasoningEffort.None.Value);
        Assert.Equal("us", CodexResidencyRequirement.Us.Value);
        Assert.Equal("untrusted", CodexApprovalPolicy.Untrusted.Value);
        Assert.Equal("on-failure", CodexApprovalPolicy.OnFailure.Value);
        Assert.Equal("danger-full-access", CodexSandboxMode.DangerFullAccess.Value);
        Assert.Equal("disabled", CodexWebSearchMode.Disabled.Value);
        Assert.Equal("cached", CodexWebSearchMode.Cached.Value);
        Assert.Equal("live", CodexWebSearchMode.Live.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("bad\0path")]
    public void MissingOrInvalidLogPath_UsesBeginningBoundary(string? path) =>
        Assert.Equal(0, StructuredOutputFileUtilities.TryGetLogByteOffset(path));

    [Theory]
    [InlineData(" \n")]
    [InlineData("")]
    public void JsonExtraction_EmptyInputHasSpecificFailure(string raw) =>
        Assert.Throws<InvalidOperationException>(() => CodexStructuredJsonExtractor.ExtractJson(raw, true));

    [Theory]
    [InlineData("42", "42")]
    [InlineData("  true  ", "true")]
    [InlineData("before {\"s\":\"brace } escaped \\\" \\\\ end\"} after", "{\"s\":\"brace } escaped \\\" \\\\ end\"}")]
    public void JsonExtraction_PreservesScalarsAndEscapedStrings(string raw, string expected) =>
        Assert.Equal(expected, CodexStructuredJsonExtractor.ExtractJson(raw, true));

    [Theory]
    [InlineData(true, 3, " Error: abc")]
    [InlineData(true, 0, " Error: ")]
    [InlineData(true, 6, " Error: abcdef")]
    [InlineData(false, 2, "text.")]
    public void RetryPrompt_RespectsErrorInclusionAndExactLimit(bool include, int max, string suffix)
    {
        var options = new CodexStructuredRetryOptions { IncludeErrorMessageInRetryPrompt = include, MaxErrorMessageChars = max };
        var prompt = options.BuildRetryPrompt(new() { Attempt = 1, MaxAttempts = 2, RawText = "bad", Exception = new Exception("abcdef") });
        Assert.EndsWith(suffix, prompt);
        if (!include) Assert.DoesNotContain("Error:", prompt);
    }

    [Fact]
    public void Schema_RejectsDictionariesAndInvalidSourceInputs()
    {
        Assert.Throws<NotSupportedException>(() => CodexJsonSchemaGenerator.Generate<Dictionary<string, int>>());
        Assert.Throws<ArgumentException>(() => CodexOutputSchema.FromFile(" "));
        Assert.Throws<ArgumentException>(() => CodexOutputSchema.FromJson(default));
    }
}
