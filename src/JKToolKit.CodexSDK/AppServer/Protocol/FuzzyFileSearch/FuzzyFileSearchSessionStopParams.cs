using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;

/// <summary>
/// Wire parameters for the <c>fuzzyFileSearch/sessionStop</c> request.
/// </summary>
public sealed record class FuzzyFileSearchSessionStopParams
{
    /// <summary>
    /// Gets the fuzzy file search session identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.SessionId)]
    public required string SessionId { get; init; }
}

