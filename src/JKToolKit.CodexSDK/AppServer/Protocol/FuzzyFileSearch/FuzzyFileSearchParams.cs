using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch</c> request.
/// </summary>
public sealed record class FuzzyFileSearchParams
{
    /// <summary>
    /// Gets the search query to match against.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Gets the roots to scan under.
    /// </summary>
    [JsonPropertyName("roots")]
    public required IReadOnlyList<string> Roots { get; init; }

    /// <summary>
    /// Gets the optional cancellation token that can cancel previous requests that shared the same value.
    /// </summary>
    [JsonPropertyName("cancellationToken")]
    public string? CancellationToken { get; init; }
}
