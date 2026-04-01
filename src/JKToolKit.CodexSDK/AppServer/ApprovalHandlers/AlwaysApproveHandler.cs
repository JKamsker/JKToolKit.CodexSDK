using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Approval handler that always approves requests.
/// </summary>
public sealed class AlwaysApproveHandler : IAppServerApprovalHandler
{
    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        var response = method switch
        {
            "item/commandExecution/requestApproval" =>
                AppServerApprovalDecisionJson.CreateCommandExecutionResponse(DeserializeOrNull<CommandExecutionRequestApprovalParams>(@params), approve: true),
            "item/fileChange/requestApproval" =>
                AppServerApprovalDecisionJson.CreateFileChangeResponse(DeserializeOrNull<FileChangeRequestApprovalParams>(@params), approve: true),
            "execCommandApproval" or "applyPatchApproval" =>
                JsonSerializer.SerializeToElement(new { decision = "approved" }),
            "item/permissions/requestApproval" => JsonSerializer.SerializeToElement(
                new PermissionsRequestApprovalResponse
                {
                    Permissions = GetRequestedPermissions(@params),
                    Scope = PermissionGrantScope.Turn
                }),
            "mcpServer/elicitation/request" => JsonSerializer.SerializeToElement(
                new McpServerElicitationRequestResponse
                {
                    Action = McpServerElicitationAction.Accept,
                    Content = GetDefaultElicitationContent(@params)
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

    private static JsonElement GetRequestedPermissions(JsonElement? @params)
    {
        if (@params is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("permissions", out var permissions) &&
            permissions.ValueKind == JsonValueKind.Object)
        {
            return permissions.Clone();
        }

        return EmptyObject();
    }

    private static JsonElement? GetDefaultElicitationContent(JsonElement? @params)
    {
        if (@params is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("mode", out var mode) &&
            mode.ValueKind == JsonValueKind.String &&
            string.Equals(mode.GetString(), "url", StringComparison.Ordinal))
        {
            return null;
        }

        return EmptyObject();
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}

