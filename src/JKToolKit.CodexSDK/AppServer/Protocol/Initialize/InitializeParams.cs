using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class InitializeParams
{
    [JsonPropertyName("clientInfo")]
    public required AppServerClientInfo ClientInfo { get; init; }

    [JsonPropertyName("capabilities")]
    public InitializeCapabilities? Capabilities { get; init; }
}

