using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire response payload for the <c>item/tool/call</c> server request (v2 protocol).
/// </summary>
public sealed record class DynamicToolCallResponse
{
    /// <summary>
    /// Gets the output content items.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ContentItems)]
    public required IReadOnlyList<DynamicToolCallOutputContentItem> ContentItems { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool call succeeded.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Success)]
    public required bool Success { get; init; }
}
