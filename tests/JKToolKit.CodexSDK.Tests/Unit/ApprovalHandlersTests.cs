using System.Linq;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ApprovalHandlers;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ApprovalHandlersTests
{
    [Theory]
    [InlineData("item/commandExecution/requestApproval", "accept")]
    [InlineData("item/fileChange/requestApproval", "accept")]
    [InlineData("execCommandApproval", "approved")]
    [InlineData("applyPatchApproval", "approved")]
    public async Task AlwaysApproveHandler_ReturnsExpectedDecision(string method, string expectedDecision)
    {
        var handler = new AlwaysApproveHandler();

        var payload = await handler.HandleAsync(method, @params: null, ct: default);

        payload.GetProperty("decision").GetString().Should().Be(expectedDecision);
    }

    [Theory]
    [InlineData("item/commandExecution/requestApproval", "decline")]
    [InlineData("item/fileChange/requestApproval", "decline")]
    [InlineData("execCommandApproval", "denied")]
    [InlineData("applyPatchApproval", "denied")]
    public async Task AlwaysDenyHandler_ReturnsExpectedDecision(string method, string expectedDecision)
    {
        var handler = new AlwaysDenyHandler();

        var payload = await handler.HandleAsync(method, @params: null, ct: default);

        payload.GetProperty("decision").GetString().Should().Be(expectedDecision);
    }

    [Fact]
    public async Task AlwaysApproveHandler_GrantsRequestedPermissions()
    {
        var handler = new AlwaysApproveHandler();
        var request = JsonDocument.Parse("""{"threadId":"thr","turnId":"turn","itemId":"item","permissions":{"fileSystem":{"write":["C:/repo"]}}}""").RootElement;

        var payload = await handler.HandleAsync("item/permissions/requestApproval", request, ct: default);
        var response = JsonSerializer.Deserialize<PermissionsRequestApprovalResponse>(payload.GetRawText())!;

        response.Scope.Should().Be(PermissionGrantScope.Turn);
        response.Permissions.GetProperty("fileSystem").GetProperty("write")[0].GetString().Should().Be("C:/repo");
    }

    [Fact]
    public async Task AlwaysDenyHandler_ReturnsEmptyPermissionGrant()
    {
        var handler = new AlwaysDenyHandler();
        var request = JsonDocument.Parse("""{"threadId":"thr","turnId":"turn","itemId":"item","permissions":{"fileSystem":{"write":["C:/repo"]}}}""").RootElement;

        var payload = await handler.HandleAsync("item/permissions/requestApproval", request, ct: default);
        var response = JsonSerializer.Deserialize<PermissionsRequestApprovalResponse>(payload.GetRawText())!;

        response.Scope.Should().Be(PermissionGrantScope.Turn);
        response.Permissions.EnumerateObject().Count().Should().Be(0);
    }

    [Fact]
    public async Task AlwaysApproveHandler_AcceptsFormElicitation()
    {
        var handler = new AlwaysApproveHandler();
        var request = JsonDocument.Parse("""{"threadId":"thr","turnId":"turn","serverName":"mcp","mode":"form","message":"Allow?","requestedSchema":{"type":"object"}}""").RootElement;

        var payload = await handler.HandleAsync("mcpServer/elicitation/request", request, ct: default);
        var response = JsonSerializer.Deserialize<McpServerElicitationRequestResponse>(payload.GetRawText())!;

        response.Action.Should().Be(McpServerElicitationAction.Accept);
        response.Content.Should().NotBeNull();
        response.Content!.Value.EnumerateObject().Count().Should().Be(0);
    }

    [Fact]
    public async Task AlwaysDenyHandler_DeclinesElicitation()
    {
        var handler = new AlwaysDenyHandler();
        var request = JsonDocument.Parse("""{"threadId":"thr","turnId":null,"serverName":"mcp","mode":"url","message":"Finish sign-in","url":"https://example.test","elicitationId":"elic-1"}""").RootElement;

        var payload = await handler.HandleAsync("mcpServer/elicitation/request", request, ct: default);
        var response = JsonSerializer.Deserialize<McpServerElicitationRequestResponse>(payload.GetRawText())!;

        response.Action.Should().Be(McpServerElicitationAction.Decline);
        response.Content.Should().BeNull();
    }
}
