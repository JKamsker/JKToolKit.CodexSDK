using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

/// <summary>
/// Wire parameters for the <c>initialize</c> request.
/// </summary>
public sealed record class InitializeParams
{
    /// <summary>
    /// Gets the client info sent to the server.
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public required AppServerClientInfo ClientInfo { get; init; }

    /// <summary>
    /// Gets optional client capabilities sent during initialization.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public InitializeCapabilities? Capabilities { get; init; }
}
