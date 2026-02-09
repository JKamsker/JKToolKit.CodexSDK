using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Describes a structured element within a text input.
/// </summary>
public sealed record class TextElement
{
    /// <summary>
    /// Gets the byte range within the parent text that this element applies to.
    /// </summary>
    [JsonPropertyName("byteRange")]
    public required ByteRange ByteRange { get; init; }

    /// <summary>
    /// Gets an optional placeholder string.
    /// </summary>
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
}
