using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Minimal envelope for a <c>fuzzyFileSearch/sessionUpdate</c> response.
/// </summary>
public sealed record class FuzzyFileSearchSessionUpdateResponse
{
    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

