using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents an image user input item in the app-server wire format.
/// </summary>
public sealed record class ImageUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>image</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "image";

    /// <summary>
    /// Gets the image URL, when the image is supplied inline/by URL.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>
    /// Gets the uploaded image file identifier, when the image is supplied by file reference.
    /// </summary>
    [JsonPropertyName("fileId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; init; }
}
