using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

using static CodexThreadItemParserJson;

internal static class CodexThreadItemMcpParser
{
    public static CodexThreadItem? Parse(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, JsonFieldNames.Server, out var server) ||
            !TryGetOptionalString(element, JsonFieldNames.EnvironmentId, out var environmentId) ||
            !TryGetRequiredString(element, JsonFieldNames.Tool, out var tool) ||
            !TryGetRequiredString(element, JsonFieldNames.Status, out var statusValue) ||
            !TryGetRequiredElement(element, JsonFieldNames.Arguments, out var arguments) ||
            !TryGetOptionalInt64(element, JsonFieldNames.DurationMs, out var durationMs))
        {
            return null;
        }

        var result = element.TryGetProperty(JsonFieldNames.Result, out var resultValue) ? resultValue.Clone() : (JsonElement?)null;
        return new CodexThreadItemMcpToolCall(
            id,
            type,
            server,
            tool,
            CodexMcpToolCallStatusExtensions.Parse(statusValue),
            arguments,
            result,
            ExtractErrorMessage(element),
            durationMs,
            raw)
        {
            EnvironmentId = environmentId
        };
    }
}
