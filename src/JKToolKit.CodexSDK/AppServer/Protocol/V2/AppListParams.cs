using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>app/list</c> request (v2 protocol).
/// </summary>
public sealed record class AppListParams
{
    /// <summary>
    /// Gets an optional cursor for paging.
    /// </summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional page size, if supported upstream.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets an optional thread identifier used to evaluate app feature gating from that thread's config.
    /// </summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets a value indicating whether to bypass caches and refetch app metadata.
    /// </summary>
    [JsonPropertyName("forceRefetch")]
    public bool? ForceRefetch { get; init; }
}

