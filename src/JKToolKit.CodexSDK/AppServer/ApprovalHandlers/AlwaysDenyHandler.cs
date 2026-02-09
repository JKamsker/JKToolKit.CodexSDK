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
        using var doc = JsonDocument.Parse("""{"approved":false}""");
        return ValueTask.FromResult(doc.RootElement.Clone());
    }
}

