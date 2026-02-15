using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>thread/archive</c> response.
/// </summary>
public sealed record class ThreadArchiveResponse
{
    /// <summary>
    /// Gets the archived thread object when present (raw).
    /// </summary>
    [JsonPropertyName("thread")]
    public JsonElement? Thread { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
