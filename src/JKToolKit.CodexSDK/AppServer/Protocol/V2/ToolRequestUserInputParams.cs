using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/tool/requestUserInput</c> server request (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.TurnId)]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the item identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ItemId)]
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the requested questions.
    /// </summary>
    [JsonPropertyName("questions")]
    public required IReadOnlyList<ToolRequestUserInputQuestion> Questions { get; init; }
}
