using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for requesting an initial turns page on <c>thread/resume</c>.
/// </summary>
public sealed record class ThreadResumeInitialTurnsPageParams
{
    /// <summary>
    /// Gets the optional page size.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets the optional sort direction. Known values are <c>asc</c> and <c>desc</c>.
    /// </summary>
    [JsonPropertyName("sortDirection")]
    public string? SortDirection { get; init; }

    /// <summary>
    /// Gets the optional item detail level. Known values are <c>summary</c> and <c>full</c>.
    /// </summary>
    [JsonPropertyName("itemsView")]
    public string? ItemsView { get; init; }
}
