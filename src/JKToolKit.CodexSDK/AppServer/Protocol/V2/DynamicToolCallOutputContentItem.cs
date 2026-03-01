using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire shape for a dynamic tool call output content item (v2 protocol).
/// </summary>
public sealed record class DynamicToolCallOutputContentItem
{
    /// <summary>
    /// Gets the content item type discriminator (for example <c>inputText</c> or <c>inputImage</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the text content (when <see cref="Type"/> is <c>inputText</c>).
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Gets the image URL (when <see cref="Type"/> is <c>inputImage</c>).
    /// </summary>
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Creates an <c>inputText</c> content item.
    /// </summary>
    public static DynamicToolCallOutputContentItem InputText(string text) =>
        new() { Type = "inputText", Text = text };

    /// <summary>
    /// Creates an <c>inputImage</c> content item.
    /// </summary>
    public static DynamicToolCallOutputContentItem InputImage(string imageUrl) =>
        new() { Type = "inputImage", ImageUrl = imageUrl };
}
