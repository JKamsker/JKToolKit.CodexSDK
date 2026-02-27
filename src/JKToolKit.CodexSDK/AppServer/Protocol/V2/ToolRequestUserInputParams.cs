using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/tool/requestUserInput</c> server request (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName("turnId")]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the item identifier.
    /// </summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the requested questions.
    /// </summary>
    [JsonPropertyName("questions")]
    public required IReadOnlyList<ToolRequestUserInputQuestion> Questions { get; init; }
}

