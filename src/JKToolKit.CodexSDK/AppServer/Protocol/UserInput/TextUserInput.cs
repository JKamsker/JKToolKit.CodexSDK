using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a text user input item in the app-server wire format.
/// </summary>
public sealed record class TextUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>text</c>).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Type)]
    public string Type => "text";

    /// <summary>
    /// Gets the plain text content.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Text)]
    public required string Text { get; init; }

    /// <summary>
    /// Gets the optional structured text elements.
    /// </summary>
    [JsonPropertyName("text_elements")]
    public IReadOnlyList<TextElement> TextElements { get; init; } = Array.Empty<TextElement>();

    /// <summary>
    /// Creates a <see cref="TextUserInput"/> from a plain string.
    /// </summary>
    public static TextUserInput Create(string text) => new() { Text = text };
}
