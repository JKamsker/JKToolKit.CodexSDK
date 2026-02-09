using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Approval handler that always approves requests.
/// </summary>
public sealed class AlwaysApproveHandler : IAppServerApprovalHandler
{
    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse("""{"approved":true}""");
        return ValueTask.FromResult(doc.RootElement.Clone());
    }
}

