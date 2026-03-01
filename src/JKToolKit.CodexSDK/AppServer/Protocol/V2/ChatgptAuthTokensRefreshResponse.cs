using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire response payload for the <c>account/chatgptAuthTokens/refresh</c> server request (v2 protocol).
/// </summary>
public sealed record class ChatgptAuthTokensRefreshResponse
{
    /// <summary>
    /// Gets the access token.
    /// </summary>
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    /// <summary>
    /// Gets the ChatGPT account/workspace identifier.
    /// </summary>
    [JsonPropertyName("chatgptAccountId")]
    public required string ChatgptAccountId { get; init; }

    /// <summary>
    /// Gets the plan type, when known.
    /// </summary>
    [JsonPropertyName("chatgptPlanType")]
    public string? ChatgptPlanType { get; init; }
}

