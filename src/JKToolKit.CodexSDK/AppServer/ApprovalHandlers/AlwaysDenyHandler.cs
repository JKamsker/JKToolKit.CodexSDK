using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
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
            AppServerMethods.ItemCommandExecutionRequestApproval =>
                AppServerApprovalDecisionJson.CreateCommandExecutionResponse(DeserializeOrNull<CommandExecutionRequestApprovalParams>(@params), approve: false),
            AppServerMethods.ItemFileChangeRequestApproval =>
                AppServerApprovalDecisionJson.CreateFileChangeResponse(DeserializeOrNull<FileChangeRequestApprovalParams>(@params), approve: false),
            "execCommandApproval" or "applyPatchApproval" =>
                JsonSerializer.SerializeToElement(new { decision = "denied" }),
            AppServerMethods.ItemPermissionsRequestApproval => JsonSerializer.SerializeToElement(
                new PermissionsRequestApprovalResponse
                {
                    Permissions = EmptyObject(),
                    Scope = PermissionGrantScope.Turn
                }),
            AppServerMethods.McpServerElicitationRequest => JsonSerializer.SerializeToElement(
                new McpServerElicitationRequestResponse
                {
                    Action = McpServerElicitationAction.Decline,
                    Content = null
                }),
            _ => throw new InvalidOperationException($"Unknown approval request method '{method}'."),
        };

        return ValueTask.FromResult(response);
    }

    private static T? DeserializeOrNull<T>(JsonElement? @params) where T : class
    {
        if (@params is null)
        {
            return null;
        }

        try
        {
            return @params.Value.Deserialize<T>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}

