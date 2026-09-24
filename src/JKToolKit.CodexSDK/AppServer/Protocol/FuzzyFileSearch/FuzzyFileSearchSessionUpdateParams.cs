using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch/sessionUpdate</c> request.
/// </summary>
public sealed record class FuzzyFileSearchSessionUpdateParams
{
    /// <summary>
    /// Gets the fuzzy file search session identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.SessionId)]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the updated query text.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Query)]
    public required string Query { get; init; }
}

