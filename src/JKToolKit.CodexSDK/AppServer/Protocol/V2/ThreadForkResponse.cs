using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Typed envelope for a <c>thread/fork</c> response.
/// </summary>
public sealed record class ThreadForkResponse
{
    /// <summary>
    /// Gets the approval policy returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalPolicy)]
    public string? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets the approval reviewer returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalsReviewer)]
    public CodexApprovalsReviewer? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets the working directory returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the model returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Model)]
    public string? Model { get; init; }

    /// <summary>
    /// Gets the model provider returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ModelProvider)]
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets the sandbox returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Sandbox)]
    public string? Sandbox { get; init; }

    /// <summary>
    /// Gets the service tier returned for the forked thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ServiceTier)]
    public string? ServiceTier { get; init; }

    /// <summary>
    /// Gets the created thread object when present (raw).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Thread)]
    public JsonElement? Thread { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
