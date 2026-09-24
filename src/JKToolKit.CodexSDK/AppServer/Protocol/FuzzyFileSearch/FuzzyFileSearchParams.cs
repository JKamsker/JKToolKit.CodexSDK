using System.Collections.Generic;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch</c> request.
/// </summary>
public sealed record class FuzzyFileSearchParams
{
    /// <summary>
    /// Gets the search query to match against.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Query)]
    public required string Query { get; init; }

    /// <summary>
    /// Gets the roots to scan under.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Roots)]
    public required IReadOnlyList<string> Roots { get; init; }

    /// <summary>
    /// Gets the optional cancellation token that can cancel previous requests that shared the same value.
    /// </summary>
    [JsonPropertyName("cancellationToken")]
    public string? CancellationToken { get; init; }
}
