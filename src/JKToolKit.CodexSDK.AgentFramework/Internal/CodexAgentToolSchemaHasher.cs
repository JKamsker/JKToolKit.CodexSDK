using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentToolSchemaHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string? Compute(IReadOnlyList<DynamicToolSpec> tools)
    {
        if (tools.Count == 0)
        {
            return null;
        }

        var payload = tools
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .Select(tool => new ToolSchema(tool.Name, tool.Description, tool.InputSchema))
            .ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record ToolSchema(
        string Name,
        string Description,
        JsonElement InputSchema);
}
