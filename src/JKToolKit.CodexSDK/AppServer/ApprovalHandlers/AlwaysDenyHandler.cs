using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Approval handler that always denies requests.
/// </summary>
public sealed class AlwaysDenyHandler : IAppServerApprovalHandler
{
    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        var decision = method switch
        {
            "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" => "decline",
            "execCommandApproval" or "applyPatchApproval" => "denied",
            _ => throw new InvalidOperationException($"Unknown approval request method '{method}'."),
        };

        return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { decision }));
    }
}

