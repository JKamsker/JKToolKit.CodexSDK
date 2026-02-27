using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire shape for an answer to a single <c>item/tool/requestUserInput</c> question (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputAnswer
{
    /// <summary>
    /// Gets the answer values.
    /// </summary>
    [JsonPropertyName("answers")]
    public required IReadOnlyList<string> Answers { get; init; }
}
