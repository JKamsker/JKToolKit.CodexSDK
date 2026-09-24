using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>account/chatgptAuthTokens/refresh</c> server request (v2 protocol).
/// </summary>
public sealed record class ChatgptAuthTokensRefreshParams
{
    /// <summary>
    /// Gets the refresh reason (wire value).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Reason)]
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the previously active account/workspace identifier, when present.
    /// </summary>
    [JsonPropertyName("previousAccountId")]
    public string? PreviousAccountId { get; init; }
}
