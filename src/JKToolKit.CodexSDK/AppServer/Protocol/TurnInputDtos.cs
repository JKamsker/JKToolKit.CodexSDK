using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class ByteRange
{
    [JsonPropertyName("start")]
    public uint Start { get; init; }

    [JsonPropertyName("end")]
    public uint End { get; init; }
}

public sealed record class TextElement
{
    [JsonPropertyName("byteRange")]
    public required ByteRange ByteRange { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
}

/// <summary>
/// V2 <c>UserInput</c> DTO used by <c>turn/start</c>.
/// </summary>
public interface IUserInput
{
    string Type { get; }
}

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

public sealed record class ImageUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "image";

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

public sealed record class LocalImageUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "localImage";

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

public sealed record class SkillUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "skill";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

public sealed record class MentionUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "mention";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
