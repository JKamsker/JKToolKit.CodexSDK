using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

public sealed class AlwaysDenyHandler : IAppServerApprovalHandler
{
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse("""{"approved":false}""");
        return ValueTask.FromResult(doc.RootElement.Clone());
    }
}

