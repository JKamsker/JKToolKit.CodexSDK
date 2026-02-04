using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record ThreadStartParams(
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("modelProvider")] string? ModelProvider = null,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("approvalPolicy")] string? ApprovalPolicy = null,
    [property: JsonPropertyName("sandbox")] string? Sandbox = null,
    [property: JsonPropertyName("config")] JsonElement? Config = null,
    [property: JsonPropertyName("baseInstructions")] string? BaseInstructions = null,
    [property: JsonPropertyName("developerInstructions")] string? DeveloperInstructions = null,
    [property: JsonPropertyName("personality")] string? Personality = null,
    [property: JsonPropertyName("ephemeral")] bool? Ephemeral = null,
    [property: JsonPropertyName("experimentalRawEvents")] bool ExperimentalRawEvents = false);

public sealed record ThreadResumeParams(
    [property: JsonPropertyName("threadId")] string ThreadId,
    [property: JsonPropertyName("history")] JsonElement? History = null,
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("modelProvider")] string? ModelProvider = null,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("approvalPolicy")] string? ApprovalPolicy = null,
    [property: JsonPropertyName("sandbox")] string? Sandbox = null,
    [property: JsonPropertyName("config")] JsonElement? Config = null,
    [property: JsonPropertyName("baseInstructions")] string? BaseInstructions = null,
    [property: JsonPropertyName("developerInstructions")] string? DeveloperInstructions = null,
    [property: JsonPropertyName("personality")] string? Personality = null);

public sealed record TurnStartParams(
    [property: JsonPropertyName("threadId")] string ThreadId,
    [property: JsonPropertyName("input")] IReadOnlyList<object> Input,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("approvalPolicy")] string? ApprovalPolicy = null,
    [property: JsonPropertyName("sandboxPolicy")] SandboxPolicy? SandboxPolicy = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("effort")] string? Effort = null,
    [property: JsonPropertyName("summary")] string? Summary = null,
    [property: JsonPropertyName("personality")] string? Personality = null,
    [property: JsonPropertyName("outputSchema")] JsonElement? OutputSchema = null,
    [property: JsonPropertyName("collaborationMode")] JsonElement? CollaborationMode = null);

public sealed record TurnInterruptParams(
    [property: JsonPropertyName("threadId")] string ThreadId,
    [property: JsonPropertyName("turnId")] string TurnId);

