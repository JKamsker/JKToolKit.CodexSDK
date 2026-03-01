using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire shape for a single selectable option in a <c>item/tool/requestUserInput</c> request (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputOption
{
    /// <summary>
    /// Gets the display label for the option.
    /// </summary>
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    /// <summary>
    /// Gets the description for the option.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
