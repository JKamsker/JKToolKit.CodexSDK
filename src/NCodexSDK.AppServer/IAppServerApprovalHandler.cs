using System.Text.Json;

namespace NCodexSDK.AppServer;

public interface IAppServerApprovalHandler
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

