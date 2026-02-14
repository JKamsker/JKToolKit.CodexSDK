using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of starting a review via the app-server.
/// </summary>
public sealed record class ReviewStartResult
{
    /// <summary>
    /// Gets the turn handle for the running review.
    /// </summary>
    public required CodexTurnHandle Turn { get; init; }

    /// <summary>
    /// Gets the detached review thread id, when detached delivery is used and provided by the server.
    /// </summary>
    public string? ReviewThreadId { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

