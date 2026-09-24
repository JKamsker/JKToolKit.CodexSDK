using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a mention user input item in the app-server wire format.
/// </summary>
public sealed record class MentionUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>mention</c>).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Type)]
    public string Type => "mention";

    /// <summary>
    /// Gets the mention display name.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Name)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file system path associated with the mention.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    public required string Path { get; init; }
}
