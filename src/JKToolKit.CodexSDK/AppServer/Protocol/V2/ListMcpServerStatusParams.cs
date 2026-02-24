using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>mcpServerStatus/list</c> request (v2 protocol).
/// </summary>
public sealed record class ListMcpServerStatusParams
{
    /// <summary>
    /// Gets an optional pagination cursor returned by a previous call.
    /// </summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional page size; defaults to a server-defined value.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }
}

