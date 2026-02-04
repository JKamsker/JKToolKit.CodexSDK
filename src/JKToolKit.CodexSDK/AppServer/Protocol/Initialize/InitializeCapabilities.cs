using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Client-declared capabilities negotiated during initialize.
/// </summary>
public sealed record class InitializeCapabilities
{
    [JsonPropertyName("experimentalApi")]
    public bool ExperimentalApi { get; init; }
}

