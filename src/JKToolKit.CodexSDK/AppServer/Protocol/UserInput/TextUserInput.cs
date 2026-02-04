using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class TextUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("text_elements")]
    public IReadOnlyList<TextElement> TextElements { get; init; } = Array.Empty<TextElement>();

    public static TextUserInput Create(string text) => new() { Text = text };
}

