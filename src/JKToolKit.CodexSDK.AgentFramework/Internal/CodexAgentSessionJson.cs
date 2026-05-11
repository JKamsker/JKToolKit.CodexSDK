using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentSessionJson
{
    public static JsonElement Serialize(CodexAgentSession session, JsonSerializerOptions? options)
    {
        return JsonSerializer.SerializeToElement(
            new SessionState(
                session.ThreadId,
                session.ToolSchemaHash,
                session.Model,
                session.Cwd,
                session.ApprovalPolicy?.Value,
                session.Sandbox?.Value,
                session.CreatedAt,
                session.StateBag.Serialize()),
            options ?? JsonSerializerOptions.Web);
    }

    public static CodexAgentSession Deserialize(JsonElement serialized)
    {
        var state = serialized.Deserialize<SessionState>(JsonSerializerOptions.Web)
            ?? new SessionState(null, null, null, null, null, null, null, default);
        var stateBag = state.StateBag.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new AgentSessionStateBag()
            : AgentSessionStateBag.Deserialize(state.StateBag);

        return new CodexAgentSession(state.ThreadId, stateBag)
        {
            ToolSchemaHash = state.ToolSchemaHash,
            Model = state.Model,
            Cwd = state.Cwd,
            ApprovalPolicy = ParseApprovalPolicy(state.ApprovalPolicy),
            Sandbox = ParseSandbox(state.Sandbox),
            CreatedAt = state.CreatedAt
        };
    }

    private static CodexApprovalPolicy? ParseApprovalPolicy(string? value)
    {
        return CodexApprovalPolicy.TryParse(value, out var parsed) ? parsed : (CodexApprovalPolicy?)null;
    }

    private static CodexSandboxMode? ParseSandbox(string? value)
    {
        return CodexSandboxMode.TryParse(value, out var parsed) ? parsed : (CodexSandboxMode?)null;
    }

    private sealed record SessionState(
        [property: JsonPropertyName("threadId")] string? ThreadId,
        [property: JsonPropertyName("toolSchemaHash")] string? ToolSchemaHash,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("cwd")] string? Cwd,
        [property: JsonPropertyName("approvalPolicy")] string? ApprovalPolicy,
        [property: JsonPropertyName("sandbox")] string? Sandbox,
        [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("stateBag")] JsonElement StateBag);
}
