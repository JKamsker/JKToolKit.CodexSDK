using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch/sessionStart</c> request.
/// </summary>
public sealed record class FuzzyFileSearchSessionStartParams
{
    /// <summary>
    /// Gets the fuzzy file search session identifier.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the roots to search under.
    /// </summary>
    [JsonPropertyName("roots")]
    public required IReadOnlyList<string> Roots { get; init; }
}

