using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Client-declared capabilities negotiated during initialize.
/// </summary>
public sealed record class InitializeCapabilities
{
    [JsonPropertyName("experimentalApi")]
    public bool ExperimentalApi { get; init; }
}

public sealed record class InitializeParams
{
    [JsonPropertyName("clientInfo")]
    public required AppServerClientInfo ClientInfo { get; init; }

    [JsonPropertyName("capabilities")]
    public InitializeCapabilities? Capabilities { get; init; }
}

public sealed record class InitializeResponse
{
    [JsonPropertyName("userAgent")]
    public required string UserAgent { get; init; }
}
