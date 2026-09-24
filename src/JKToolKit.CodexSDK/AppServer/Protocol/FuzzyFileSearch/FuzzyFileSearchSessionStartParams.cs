using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch/sessionStart</c> request.
/// </summary>
public sealed record class FuzzyFileSearchSessionStartParams
{
    /// <summary>
    /// Gets the fuzzy file search session identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.SessionId)]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the roots to search under.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Roots)]
    public required IReadOnlyList<string> Roots { get; init; }
}

