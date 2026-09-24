using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a local image user input item in the app-server wire format.
/// </summary>
public sealed record class LocalImageUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>localImage</c>).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Type)]
    public string Type => "localImage";

    /// <summary>
    /// Gets the local image path.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    public required string Path { get; init; }
}
