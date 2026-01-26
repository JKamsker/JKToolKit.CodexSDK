using System.Text.Json;

namespace NCodexSDK.McpServer;

public interface IMcpElicitationHandler
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

