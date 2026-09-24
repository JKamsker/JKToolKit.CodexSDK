using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

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
    /// Gets an optional section filter. Omit to include every section; send JSON null to include only unsectioned threads.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.SectionId)]
    public JsonElement? SectionId { get; init; }

    /// <summary>
    /// Gets an optional project filter. Omit to include every project; send JSON null to include only unassigned threads.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ProjectId)]
    public JsonElement? ProjectId { get; init; }

    /// <summary>
    /// Gets an optional working directory filter.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional substring filter for the extracted thread title, if supported upstream.
    /// </summary>
    [JsonPropertyName("searchTerm")]
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets an optional limit (page size), if supported upstream.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Limit)]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets an optional model provider filter, if supported upstream.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ModelProviders)]
    public IReadOnlyList<string>? ModelProviders { get; init; }

    /// <summary>
    /// Gets an optional source kind filter, if supported upstream.
    /// </summary>
    [JsonPropertyName("sourceKinds")]
    public IReadOnlyList<string>? SourceKinds { get; init; }

    /// <summary>
    /// Gets an optional originator allowlist, if supported upstream.
    /// </summary>
    [JsonPropertyName("originators")]
    public IReadOnlyList<string>? Originators { get; init; }

    /// <summary>
    /// Gets an optional cursor for paging.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cursor)]
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets an optional sort key, if supported upstream.
    /// </summary>
    [JsonPropertyName("sortKey")]
    public string? SortKey { get; init; }
}
