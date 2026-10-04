using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerJsonHelperContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("{\"wanted\":\"direct\",\"nested\":{\"wanted\":\"other\"}}", 0, "direct")]
    [InlineData("{\"wanted\":\" \",\"skip\":null,\"nested\":{\"wanted\":\"deep\"}}", 1, "deep")]
    [InlineData("{\"wanted\":false,\"nested\":[null,{}, {\"wanted\":\"deep\"}]}", 2, "deep")]
    [InlineData("[{\"wanted\":\"\"},{\"wanted\":\"second\"}]", 1, "second")]
    [InlineData("[null,{},false,[]]", 6, null)]
    [InlineData("{\"nested\":{\"wanted\":\"too-deep\"}}", 0, null)]
    [InlineData("{\"wanted\":\"direct\"}", -1, null)]
    [InlineData("42", 2, null)]
    [InlineData("[{\"wanted\":\"too-deep\"}]", 0, null)]
    public void RecursiveLookup_RespectsDepthFirstPrecedenceAndExactDepthLimit(string json, int depth, string? expected) =>
        CodexAppServerClientJson.FindStringPropertyRecursive(Json(json), "wanted", depth).Should().Be(expected);

    [Theory]
    [InlineData("{\"threadId\":\"thread\",\"turnId\":\"turn\",\"id\":\"fallback\"}", "thread", "turn")]
    [InlineData("{\"id\":\"fallback\",\"thread\":{\"id\":\"nested\"},\"turn\":{\"id\":\"nested\"}}", "fallback", "fallback")]
    [InlineData("{\"thread\":{\"threadId\":\"thread\",\"id\":\"ignored\"},\"turn\":{\"turnId\":\"turn\",\"id\":\"ignored\"}}", "thread", "turn")]
    [InlineData("{\"thread\":{\"id\":\"thread\"},\"turn\":{\"id\":\"turn\"}}", "thread", "turn")]
    [InlineData("{\"events\":[null,{\"threadId\":\"thread\",\"turnId\":\"turn\"}]}", "thread", "turn")]
    [InlineData("{\"thread\":false,\"turn\":[]}", null, null)]
    [InlineData("null", null, null)]
    [InlineData("{\"threadId\":42,\"turnId\":false,\"id\":\"fallback\"}", "fallback", "fallback")]
    [InlineData("{\"thread\":{\"id\":\"thread\"},\"turn\":{\"id\":\"turn\"},\"other\":{\"threadId\":\"ignored\",\"turnId\":\"ignored\"}}", "thread", "turn")]
    public void IdentifierExtraction_PrioritizesKnownEnvelopesBeforeRecursiveFallback(string json, string? threadId, string? turnId)
    {
        var payload = Json(json);
        CodexAppServerClientJson.ExtractThreadId(payload).Should().Be(threadId);
        CodexAppServerClientJson.ExtractTurnId(payload).Should().Be(turnId);
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("{}", null)]
    [InlineData("{\"value\":null}", null)]
    [InlineData("{\"value\":false}", null)]
    [InlineData("{\"value\":1.5}", null)]
    [InlineData("{\"value\":\"bad\"}", null)]
    [InlineData("{\"value\":2147483648}", null)]
    [InlineData("{\"value\":2147483647}", 2147483647)]
    [InlineData("{\"value\":\"-2147483648\"}", -2147483648)]
    public void OptionalInteger_ConvertsLegacyStringsButRejectsFractionalAndOverflowingValues(string json, int? expected) =>
        CodexAppServerClientJson.GetInt32OrNull(Json(json), "value").Should().Be(expected);

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"value\":null}")]
    [InlineData("{\"value\":\"1\"}")]
    [InlineData("{\"value\":1.5}")]
    [InlineData("{\"value\":9223372036854775808}")]
    public void RequiredNumbers_RejectWrongTypesAndReportCallerContext(string json)
    {
        var payload = Json(json);
        Action int32 = () => CodexAppServerClientJson.GetRequiredInt32(payload, "value", "test response");
        Action int64 = () => CodexAppServerClientJson.GetRequiredInt64(payload, "value");
        int32.Should().Throw<InvalidOperationException>().WithMessage("Missing required property 'value' on test response.");
        int64.Should().Throw<InvalidOperationException>().WithMessage("Missing required property 'value' on payload.");
    }

    [Fact]
    public void RequiredAndOptionalFields_KeepNullWrongTypesAndEmptyArraysDistinct()
    {
        foreach (var json in new[] { "null", "{}", "{\"value\":null}", "{\"value\":42}" })
        {
            var payload = Json(json);
            CodexAppServerClientJson.GetBoolOrNull(payload, "value").Should().BeNull();
            CodexAppServerClientJson.GetOptionalStringArray(payload, "value").Should().BeNull();
            Action boolean = () => CodexAppServerClientJson.GetRequiredBool(payload, "value");
            boolean.Should().Throw<InvalidOperationException>().WithMessage("Missing required property 'value' on payload.");
            CodexAppServerClientJson.TryGetArray(payload, "value").Should().BeNull();
            CodexAppServerClientJson.TryGetObject(payload, "value").Should().BeNull();
        }
        CodexAppServerClientJson.GetOptionalStringArray(Json("{\"value\":[]}"), "value").Should().BeEmpty();
        CodexAppServerClientJson.GetOptionalStringArray(Json("{\"value\":[\"first\",null,3,\"\",\"last\"]}"), "value").Should().Equal("first", "", "last");
        CodexAppServerClientJson.GetRequiredBool(Json("{\"value\":false}"), "value").Should().BeFalse();
        CodexAppServerClientJson.GetRequiredBool(Json("{\"value\":true}"), "value").Should().BeTrue();
        CodexAppServerClientJson.GetRequiredInt32(Json("{\"value\":2147483647}"), "value").Should().Be(int.MaxValue);
        CodexAppServerClientJson.GetRequiredInt64(Json("{\"value\":9223372036854775807}"), "value").Should().Be(long.MaxValue);
        CodexAppServerClientJson.GetInt64OrNull(Json("{\"value\":\"9223372036854775807\"}"), "value").Should().Be(long.MaxValue);
        CodexAppServerClientJson.GetInt64OrNull(Json("{\"value\":\"9223372036854775808\"}"), "value").Should().BeNull();
        JsonElement? clone;
        using (var doc = JsonDocument.Parse("{\"value\":{\"nested\":[1]}}"))
            clone = CodexAppServerClientJson.TryGetElement(doc.RootElement, "value");
        clone!.Value.GetProperty("nested")[0].GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("\"2025-01-02T03:04:05+02:00\"", "2025-01-02T01:04:05Z")]
    [InlineData("10000000000", "2286-11-20T17:46:40Z")]
    [InlineData("10000000001", "1970-04-26T17:46:40.001Z")]
    [InlineData("-1", "1969-12-31T23:59:59Z")]
    public void Timestamps_AcceptRoundTripDatesAndDistinguishSecondsFromMilliseconds(string value, string expected) =>
        CodexAppServerClientJson.GetDateTimeOffsetOrNull(Json("{\"value\":" + value + "}"), "value").Should().Be(DateTimeOffset.Parse(expected));

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"value\":false}")]
    [InlineData("{\"value\":\" \"}")]
    [InlineData("{\"value\":\"invalid\"}")]
    [InlineData("{\"value\":1.5}")]
    public void InvalidOptionalTimestamps_RemainAbsent(string json) =>
        CodexAppServerClientJson.GetDateTimeOffsetOrNull(Json(json), "value").Should().BeNull();

    [Theory]
    [InlineData("Unknown field readOnlyAccess", true)]
    [InlineData("UNRECOGNIZED FIELD read_only_access", true)]
    [InlineData("unknown property readonly_access", true)]
    [InlineData("unexpected property readonlyaccess", true)]
    [InlineData("additional properties sandboxPolicy.access", true)]
    [InlineData("sandbox_policy.access was unexpected", true)]
    [InlineData("unknown field /sandboxPolicy/access", true)]
    [InlineData("unknown field /sandbox_policy/access", true)]
    [InlineData("/sandboxPolicy/readOnlyAccess", true)]
    [InlineData("/sandbox_policy/read_only_access", true)]
    [InlineData("unknown field unrelated", false)]
    [InlineData("readOnlyAccess denied", false)]
    [InlineData("sandboxPolicy.access denied", false)]
    [InlineData("/sandboxPolicy/unrelated denied", false)]
    [InlineData("", false)]
    public void ReadOnlyOverrideRejection_RequiresARecognizedFieldAndCompatibilitySignal(string message, bool expected)
    {
        var error = new JsonRpcRemoteException(new JsonRpcError(-32602, message));
        CodexAppServerReadOnlyAccessOverridesSupport.ShouldMarkRejected(error).Should().Be(expected);
        var dataError = new JsonRpcRemoteException(new JsonRpcError(-32602, "Invalid parameters", JsonSerializer.SerializeToElement(new { detail = message })));
        CodexAppServerReadOnlyAccessOverridesSupport.ShouldMarkRejected(dataError).Should().Be(expected);
    }

    [Fact]
    public void RejectionDetection_TreatsNullAndUndefinedErrorDataAsAbsent()
    {
        foreach (var data in new JsonElement?[] { null, default(JsonElement), Json("null") })
            CodexAppServerReadOnlyAccessOverridesSupport.ShouldMarkRejected(new JsonRpcRemoteException(new JsonRpcError(1, "unrelated", data))).Should().BeFalse();
    }
    [Fact]
    public void RejectionDetection_IgnoresCompatibilityPhrasesOutsideTheBoundedErrorDataExcerpt()
    {
        var data = JsonSerializer.SerializeToElement(new { details = new string('x', 2100) + " unknown field readOnlyAccess" });
        var error = new JsonRpcRemoteException(new JsonRpcError(-32602, "Invalid parameters", data));
        CodexAppServerReadOnlyAccessOverridesSupport.ShouldMarkRejected(error).Should().BeFalse();
        var truncated = JsonSerializer.SerializeToElement(new { details = "unknown field readOnlyAccess " + new string('x', 2100) });
        CodexAppServerReadOnlyAccessOverridesSupport.ShouldMarkRejected(new JsonRpcRemoteException(new JsonRpcError(-32602, "Invalid parameters", truncated))).Should().BeTrue();
    }

}
