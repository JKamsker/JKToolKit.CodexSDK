using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of reading a thread via the app-server.
/// </summary>
public sealed record class CodexThreadReadResult
{
    /// <summary>
    /// Gets the parsed thread summary.
    /// </summary>
    public required CodexThreadSummary Thread { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

