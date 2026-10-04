using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;
using JKToolKit.CodexSDK.AppServer.Protocol.UserInput;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class HandwrittenWireDtoContractTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Theory]
    [InlineData(typeof(AppListParams), """{"cursor":"apps-page-2","limit":25,"threadId":"thread-1","forceRefetch":true}""")]
    [InlineData(typeof(ThreadLoadedListParams), """{"cursor":"loaded-page-2","limit":12}""")]
    [InlineData(typeof(ThreadArchiveParams), """{"threadId":"thread-archive"}""")]
    [InlineData(typeof(ThreadUnarchiveParams), """{"threadId":"thread-restore"}""")]
    [InlineData(typeof(ThreadCompactStartParams), """{"threadId":"thread-compact"}""")]
    [InlineData(typeof(TurnInterruptParams), """{"threadId":"thread-1","turnId":"turn-2"}""")]
    [InlineData(typeof(InitializeResponse), """{"userAgent":"codex/0.152.0"}""")]
    [InlineData(typeof(MentionUserInput), """{"type":"mention","name":"design notes","path":"/workspace/design.md"}""")]
    [InlineData(typeof(SkillUserInput), """{"type":"skill","name":"review","path":"/workspace/skills/review/SKILL.md"}""")]
    [InlineData(typeof(TextUserInput), """{"type":"text","text":"Review @design","text_elements":[{"byteRange":{"start":7,"end":14},"placeholder":"@design"}]}""")]
    public void RequestAndInputDtos_RoundTripPublishedWireNames(Type dtoType, string wire)
    {
        AssertWireRoundTrip(dtoType, wire);
    }

    [Theory]
    [InlineData(typeof(AppListResponse), """{"data":[{"id":"app-1","name":"Calendar"}],"nextCursor":"apps-page-2","future":{"enabled":true}}""")]
    [InlineData(typeof(ThreadListResponse), """{"data":[{"id":"thread-1","preview":"Review"}],"nextCursor":"threads-page-2","future":{"pageVersion":3}}""")]
    [InlineData(typeof(ThreadLoadedListResponse), """{"data":["thread-1","thread-2"],"nextCursor":"loaded-page-2","future":{"pageVersion":3}}""")]
    [InlineData(typeof(SkillsListResponse), """{"data":[{"cwd":"/workspace","skills":[{"name":"review"}]}],"future":{"source":"cache"}}""")]
    [InlineData(typeof(SkillsRemoteReadResponse), """{"data":[{"id":"skill-1","name":"review"}],"future":{"source":"registry"}}""")]
    [InlineData(typeof(SkillsRemoteWriteResponse), """{"id":"skill-1","name":"review","path":"/workspace/skills/review","future":{"updated":true}}""")]
    [InlineData(typeof(SkillsConfigWriteResponse), """{"effectiveEnabled":false,"future":{"scope":"workspace"}}""")]
    [InlineData(typeof(ThreadRollbackResponse), """{"thread":{"id":"thread-1","turns":[]},"future":{"removedTurns":2}}""")]
    [InlineData(typeof(ThreadResumeResponse), """{"thread":{"id":"thread-1"},"turnsBackwardsCursor":"turn-page-2","itemsBackwardsCursor":"item-page-2","future":{"resumed":true}}""")]
    [InlineData(typeof(ThreadArchiveResponse), """{"future":{"archived":true}}""")]
    [InlineData(typeof(ThreadCompactStartResponse), """{"future":{"scheduled":true}}""")]
    [InlineData(typeof(ThreadBackgroundTerminalsCleanResponse), """{"future":{"terminated":2}}""")]
    [InlineData(typeof(ThreadSetNameResponse), """{"future":{"renamed":true}}""")]
    [InlineData(typeof(FuzzyFileSearchSessionStartResponse), """{"future":{"sessionId":"search-1"}}""")]
    [InlineData(typeof(FuzzyFileSearchSessionUpdateResponse), """{"future":{"sequence":2}}""")]
    [InlineData(typeof(FuzzyFileSearchSessionStopResponse), """{"future":{"stopped":true}}""")]
    public void ResponseEnvelopes_PreserveKnownPayloadAndFutureProperties(Type dtoType, string wire)
    {
        AssertWireRoundTrip(dtoType, wire);
    }

    [Fact]
    public void CommandApproval_RetainsActionAndPolicyUnionPayloads()
    {
        const string wire = """
            {"kind":"command","threadId":"thread-1","turnId":"turn-1","itemId":"item-1",
             "approvalId":"approval-1","reason":"Read requested document","command":"cat notes.txt","cwd":"/workspace",
             "networkApprovalContext":{"host":"example.test","protocol":"https"},
             "commandActions":[{"type":"read","path":"notes.txt"}],
             "additionalPermissions":{"network":{"enabled":true}},
             "proposedExecpolicyAmendment":["cat","notes.txt"],
             "proposedNetworkPolicyAmendments":[{"host":"example.test","action":"allow"}],
             "availableDecisions":["accept",{"acceptWithExecpolicyAmendment":{"execpolicyAmendment":["cat"]}}]}
            """;
        var request = JsonSerializer.Deserialize<CommandExecutionRequestApprovalParams>(wire)!;
        request.CommandActions!.Single().GetProperty("path").GetString().Should().Be("notes.txt");
        request.AdditionalPermissions!.Value.GetProperty("network").GetProperty("enabled").GetBoolean().Should().BeTrue();
        request.ProposedExecpolicyAmendment!.Value[0].GetString().Should().Be("cat");
        request.ProposedNetworkPolicyAmendments!.Single().GetProperty("action").GetString().Should().Be("allow");
        request.AvailableDecisions!.Should().HaveCount(2);
        AssertWireRoundTrip(typeof(CommandExecutionRequestApprovalParams), wire);
    }

    [Theory]
    [InlineData("turn", PermissionGrantScope.Turn)]
    [InlineData("session", PermissionGrantScope.Session)]
    public void PermissionGrants_RoundTripScopeAndPermissionSubset(string scope, PermissionGrantScope expected)
    {
        var wire = """{"permissions":{"network":{"enabled":true}},"scope":"SCOPE"}""".Replace("SCOPE", scope, StringComparison.Ordinal);
        var grant = JsonSerializer.Deserialize<PermissionsRequestApprovalResponse>(wire)!;
        grant.Scope.Should().Be(expected);
        grant.Permissions.GetProperty("network").GetProperty("enabled").GetBoolean().Should().BeTrue();
        AssertWireRoundTrip(typeof(PermissionsRequestApprovalResponse), wire);
    }

    [Fact]
    public void PermissionGrants_RejectUnknownScopes_InBothDirections()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PermissionGrantScope>("\"future-scope\""))
            .Message.Should().Contain("future-scope");
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((PermissionGrantScope)42))
            .Message.Should().Contain("42");
    }

    private static void AssertWireRoundTrip(Type dtoType, string wire)
    {
        var value = JsonSerializer.Deserialize(wire, dtoType, Options);
        value.Should().NotBeNull();
        var serialized = JsonSerializer.SerializeToElement(value, dtoType, Options);
        JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(wire), serialized).Should().BeTrue(
            "{0} must retain its wire property names, nested payloads, and extension data", dtoType.Name);
    }
}
