using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of a <c>thread/unsubscribe</c> request.
/// </summary>
public sealed record class ThreadUnsubscribeResult
{
    /// <summary>
    /// Gets the unsubscribe status.
    /// </summary>
    public required ThreadUnsubscribeStatus Status { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

