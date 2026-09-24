using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>mcpServerStatus/list</c> request (v2 protocol).
/// </summary>
public sealed record class ListMcpServerStatusParams
{
    /// <summary>
    /// Gets an optional pagination cursor returned by a previous call.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cursor)]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional page size; defaults to a server-defined value.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Limit)]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets the optional MCP inventory detail level.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Detail)]
    public string? Detail { get; init; }

    /// <summary>
    /// Gets an optional thread id used to include thread/project-scoped MCP configuration.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public string? ThreadId { get; init; }
}

