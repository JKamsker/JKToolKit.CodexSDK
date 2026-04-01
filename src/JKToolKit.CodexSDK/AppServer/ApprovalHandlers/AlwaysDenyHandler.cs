using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Approval handler that always denies requests.
/// </summary>
public sealed class AlwaysDenyHandler : IAppServerApprovalHandler
{
    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        var response = method switch
        {
            "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" =>
                JsonSerializer.SerializeToElement(new { decision = "decline" }),
            "execCommandApproval" or "applyPatchApproval" =>
                JsonSerializer.SerializeToElement(new { decision = "denied" }),
            "item/permissions/requestApproval" => JsonSerializer.SerializeToElement(
                new PermissionsRequestApprovalResponse
                {
                    Permissions = EmptyObject(),
                    Scope = PermissionGrantScope.Turn
                }),
            "mcpServer/elicitation/request" => JsonSerializer.SerializeToElement(
                new McpServerElicitationRequestResponse
                {
                    Action = McpServerElicitationAction.Decline,
                    Content = null
                }),
            _ => throw new InvalidOperationException($"Unknown approval request method '{method}'."),
        };

        return ValueTask.FromResult(response);
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}

