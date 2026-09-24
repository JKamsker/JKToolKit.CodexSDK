using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Typed envelope for a <c>thread/resume</c> response.
/// </summary>
public sealed record class ThreadResumeResponse
{
    /// <summary>
    /// Gets the approval policy returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalPolicy)]
    public string? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets the approval reviewer returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalsReviewer)]
    public CodexApprovalsReviewer? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets the working directory returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the model returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Model)]
    public string? Model { get; init; }

    /// <summary>
    /// Gets the model provider returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ModelProvider)]
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets the sandbox returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Sandbox)]
    public string? Sandbox { get; init; }

    /// <summary>
    /// Gets the service tier returned for the resumed thread, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ServiceTier)]
    public string? ServiceTier { get; init; }

    /// <summary>
    /// Gets the resumed thread object when present (raw).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Thread)]
    public JsonElement? Thread { get; init; }

    /// <summary>
    /// Gets the cursor for hydrating paginated turns backwards, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.TurnsBackwardsCursor)]
    public string? TurnsBackwardsCursor { get; init; }

    /// <summary>
    /// Gets the cursor for hydrating paginated items backwards, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ItemsBackwardsCursor)]
    public string? ItemsBackwardsCursor { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
