using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ApprovalDecisionEdgeTests
{
    [Theory]
    [InlineData(true, "item/commandExecution/requestApproval", "accept")]
    [InlineData(false, "item/commandExecution/requestApproval", "decline")]
    [InlineData(true, "item/fileChange/requestApproval", "accept")]
    [InlineData(false, "item/fileChange/requestApproval", "decline")]
    public async Task AutomaticHandlers_MalformedOrEmptyParamsUsePolicy(bool approve, string method, string expected)
    {
        IAppServerApprovalHandler handler = approve ? new AlwaysApproveHandler() : new AlwaysDenyHandler();
        foreach (var json in new[] { "[]", "null", "{}", "{\"threadId\":\"t\",\"turnId\":\"u\",\"itemId\":\"i\",\"availableDecisions\":[]}" })
        {
            var result = await handler.HandleAsync(method, JsonSerializer.Deserialize<JsonElement>(json), default);
            result.GetProperty("decision").GetString().Should().Be(expected);
        }
    }

    [Theory]
    [InlineData(true, "[\"acceptForSession\",\"accept\"]", "\"accept\"")]
    [InlineData(true, "[\"decline\",\"acceptForSession\"]", "\"acceptForSession\"")]
    [InlineData(true, "[{\"applyNetworkPolicyAmendment\":{\"host\":\"example.test\"}}]", "{\"applyNetworkPolicyAmendment\":{\"host\":\"example.test\"}}")]
    [InlineData(false, "[\"accept\",\"cancel\"]", "\"cancel\"")]
    [InlineData(false, "[\"cancel\",\"decline\"]", "\"decline\"")]
    [InlineData(true, "[{},42,null]", "{}")]
    [InlineData(false, "[42,{}]", "42")]
    public async Task AutomaticCommandDecision_UsesPreferenceOrderAndPreservesWireValue(bool approve, string choices, string expected)
    {
        IAppServerApprovalHandler handler = approve ? new AlwaysApproveHandler() : new AlwaysDenyHandler();
        var payload = JsonSerializer.Deserialize<JsonElement>("{\"threadId\":\"t\",\"turnId\":\"u\",\"itemId\":\"i\",\"availableDecisions\":" + choices + "}");
        var result = await handler.HandleAsync("item/commandExecution/requestApproval", payload, default);
        result.GetProperty("decision").GetRawText().Should().Be(expected);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"permissions\":null}")]
    [InlineData("{\"permissions\":42}")]
    public async Task AutomaticApproval_MissingOrInvalidPermissionsGrantsEmptyObject(string json)
    {
        var result = await new AlwaysApproveHandler().HandleAsync("item/permissions/requestApproval", JsonSerializer.Deserialize<JsonElement>(json), default);
        result.GetProperty("permissions").EnumerateObject().Should().BeEmpty();
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    [InlineData("{\"mode\":42}", false)]
    [InlineData("{\"mode\":\"form\"}", false)]
    [InlineData("{\"mode\":\"url\"}", true)]
    public async Task AutomaticElicitation_DefaultContentDependsOnMode(string json, bool isNull)
    {
        var result = await new AlwaysApproveHandler().HandleAsync("mcpServer/elicitation/request", JsonSerializer.Deserialize<JsonElement>(json), default);
        result.GetProperty("content").ValueKind.Should().Be(isNull ? JsonValueKind.Null : JsonValueKind.Object);
    }

    [Theory]
    [InlineData("\"accept\"", "accept")]
    [InlineData("{\"acceptForSession\":{}}", "acceptForSession")]
    [InlineData("{}", "{}")]
    [InlineData("null", "null")]
    [InlineData("42", "42")]
    public void DecisionDescription_ShowsKindOrRawUnknownValue(string json, string expected) =>
        AppServerApprovalDecisionJson.DescribeDecision(JsonSerializer.Deserialize<JsonElement>(json)).Should().Be(expected);

    [Fact]
    public async Task AutomaticHandlers_RejectUnknownMethod()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AlwaysApproveHandler().HandleAsync("unknown", null, default).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AlwaysDenyHandler().HandleAsync("unknown", null, default).AsTask());
    }
}
