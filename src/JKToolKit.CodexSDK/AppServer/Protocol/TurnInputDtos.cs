using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record ByteRange(
    [property: JsonPropertyName("start")] uint Start,
    [property: JsonPropertyName("end")] uint End);

public sealed record TextElement(
    [property: JsonPropertyName("byteRange")] ByteRange ByteRange,
    [property: JsonPropertyName("placeholder")] string? Placeholder);

/// <summary>
/// V2 <c>UserInput</c> DTO used by <c>turn/start</c>.
/// </summary>
public interface IUserInput
{
    string Type { get; }
}

public sealed record TextUserInput(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("text_elements")] IReadOnlyList<TextElement> TextElements)
    : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "text";

    public static TextUserInput Create(string text) => new(text, Array.Empty<TextElement>());
}

public sealed record ImageUserInput(
    [property: JsonPropertyName("url")] string Url)
    : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "image";
}

public sealed record LocalImageUserInput(
    [property: JsonPropertyName("path")] string Path)
    : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "localImage";
}

public sealed record SkillUserInput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path)
    : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "skill";
}

public sealed record MentionUserInput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path)
    : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "mention";
}

