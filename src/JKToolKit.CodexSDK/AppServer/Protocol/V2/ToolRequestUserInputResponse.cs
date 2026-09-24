using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire response payload for the <c>item/tool/requestUserInput</c> server request (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputResponse
{
    /// <summary>
    /// Gets the mapping from question id to answers.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Answers)]
    public required IReadOnlyDictionary<string, ToolRequestUserInputAnswer> Answers { get; init; }
}
