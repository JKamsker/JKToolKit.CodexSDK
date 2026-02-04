using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class TurnStartParams
{
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("input")]
    public required IReadOnlyList<object> Input { get; init; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    [JsonPropertyName("approvalPolicy")]
    public string? ApprovalPolicy { get; init; }

    [JsonPropertyName("sandboxPolicy")]
    public SandboxPolicy? SandboxPolicy { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("effort")]
    public string? Effort { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("personality")]
    public string? Personality { get; init; }

    [JsonPropertyName("outputSchema")]
    public JsonElement? OutputSchema { get; init; }

    [JsonPropertyName("collaborationMode")]
    public JsonElement? CollaborationMode { get; init; }
}

