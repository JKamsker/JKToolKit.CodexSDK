using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

public interface IMcpElicitationHandler
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

