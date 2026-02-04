using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Client-declared capabilities negotiated during initialize.
/// </summary>
public sealed record InitializeCapabilities(
    [property: JsonPropertyName("experimentalApi")] bool ExperimentalApi);

public sealed record InitializeParams(
    [property: JsonPropertyName("clientInfo")] AppServerClientInfo ClientInfo,
    [property: JsonPropertyName("capabilities")] InitializeCapabilities? Capabilities);

public sealed record InitializeResponse(
    [property: JsonPropertyName("userAgent")] string UserAgent);

