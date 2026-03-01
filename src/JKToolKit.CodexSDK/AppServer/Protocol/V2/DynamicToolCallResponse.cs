using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire response payload for the <c>item/tool/call</c> server request (v2 protocol).
/// </summary>
public sealed record class DynamicToolCallResponse
{
    /// <summary>
    /// Gets the output content items.
    /// </summary>
    [JsonPropertyName("contentItems")]
    public required IReadOnlyList<DynamicToolCallOutputContentItem> ContentItems { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool call succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }
}

