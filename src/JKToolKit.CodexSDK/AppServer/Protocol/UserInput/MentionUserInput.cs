using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a mention user input item in the app-server wire format.
/// </summary>
public sealed record class MentionUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>mention</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "mention";

    /// <summary>
    /// Gets the mention display name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file system path associated with the mention.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
