using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the raw and extracted result of a <c>turn/steer</c> request.
/// </summary>
public sealed record class TurnSteerResult
{
    /// <summary>
    /// Gets the confirmed (or newly assigned) turn identifier.
    /// </summary>
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the raw JSON-RPC result payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

