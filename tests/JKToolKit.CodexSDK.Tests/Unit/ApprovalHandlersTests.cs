using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

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
}
