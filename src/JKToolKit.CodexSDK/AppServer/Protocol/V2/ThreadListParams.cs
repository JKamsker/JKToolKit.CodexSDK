using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/list</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadListParams
{
    /// <summary>
    /// Gets an optional archived filter.
    /// </summary>
    [JsonPropertyName("archived")]
    public bool? Archived { get; init; }

    /// <summary>
    /// Gets an optional working directory filter.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional search query filter, if supported upstream.
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>
    /// Gets an optional page size, if supported upstream.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    /// <summary>
    /// Gets an optional cursor for paging.
    /// </summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional sort key, if supported upstream.
    /// </summary>
    [JsonPropertyName("sortKey")]
    public string? SortKey { get; init; }

    /// <summary>
    /// Gets an optional sort direction (e.g. "asc"/"desc"), if supported upstream.
    /// </summary>
    [JsonPropertyName("sortDirection")]
    public string? SortDirection { get; init; }
}
