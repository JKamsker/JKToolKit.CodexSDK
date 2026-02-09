using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a local image user input item in the app-server wire format.
/// </summary>
public sealed record class LocalImageUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>localImage</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "localImage";

    /// <summary>
    /// Gets the local image path.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
