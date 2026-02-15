using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>thread/rollback</c> response.
/// </summary>
public sealed record class ThreadRollbackResponse
{
    /// <summary>
    /// Gets the thread object when present (raw).
    /// </summary>
    [JsonPropertyName("thread")]
    public JsonElement? Thread { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

