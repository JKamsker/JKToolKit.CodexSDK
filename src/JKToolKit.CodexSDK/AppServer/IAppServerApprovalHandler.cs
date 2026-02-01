using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

public interface IAppServerApprovalHandler
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

