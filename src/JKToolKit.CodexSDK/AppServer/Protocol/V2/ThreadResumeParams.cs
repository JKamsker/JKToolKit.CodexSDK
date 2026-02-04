using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class ThreadResumeParams
{
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("history")]
    public JsonElement? History { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("modelProvider")]
    public string? ModelProvider { get; init; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    [JsonPropertyName("approvalPolicy")]
    public string? ApprovalPolicy { get; init; }

    [JsonPropertyName("sandbox")]
    public string? Sandbox { get; init; }

    [JsonPropertyName("config")]
    public JsonElement? Config { get; init; }

    [JsonPropertyName("baseInstructions")]
    public string? BaseInstructions { get; init; }

    [JsonPropertyName("developerInstructions")]
    public string? DeveloperInstructions { get; init; }

    [JsonPropertyName("personality")]
    public string? Personality { get; init; }
}

