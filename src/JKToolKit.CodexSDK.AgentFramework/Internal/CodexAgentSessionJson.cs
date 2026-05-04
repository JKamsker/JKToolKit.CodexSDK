using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentSessionJson
{
    public static JsonElement Serialize(CodexAgentSession session, JsonSerializerOptions? options)
    {
        return JsonSerializer.SerializeToElement(
            new SessionState(session.ThreadId, session.StateBag.Serialize()),
            options ?? JsonSerializerOptions.Web);
    }

    public static CodexAgentSession Deserialize(JsonElement serialized)
    {
        var state = serialized.Deserialize<SessionState>(JsonSerializerOptions.Web)
            ?? new SessionState(null, default);
        var stateBag = state.StateBag.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new AgentSessionStateBag()
            : AgentSessionStateBag.Deserialize(state.StateBag);

        return new CodexAgentSession(state.ThreadId, stateBag);
    }

    private sealed record SessionState(
        [property: JsonPropertyName("threadId")] string? ThreadId,
        [property: JsonPropertyName("stateBag")] JsonElement StateBag);
}
