using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire response payload for the <c>item/tool/requestUserInput</c> server request (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputResponse
{
    /// <summary>
    /// Gets the mapping from question id to answers.
    /// </summary>
    [JsonPropertyName("answers")]
    public required IReadOnlyDictionary<string, ToolRequestUserInputAnswer> Answers { get; init; }
}
