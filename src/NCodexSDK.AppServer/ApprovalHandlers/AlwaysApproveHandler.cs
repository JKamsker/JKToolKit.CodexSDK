using System.Text.Json;

namespace NCodexSDK.AppServer.ApprovalHandlers;

public sealed class AlwaysApproveHandler : IAppServerApprovalHandler
{
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse("""{"approved":true}""");
        return ValueTask.FromResult(doc.RootElement.Clone());
    }
}

