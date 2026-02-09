using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents an image-by-URL user input item in the app-server wire format.
/// </summary>
public sealed record class ImageUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>image</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "image";

    /// <summary>
    /// Gets the image URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
