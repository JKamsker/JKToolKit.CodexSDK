using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of listing apps/connectors via the app-server.
/// </summary>
public sealed record class AppsListResult
{
    /// <summary>
    /// Gets the returned apps/connectors.
    /// </summary>
    public required IReadOnlyList<AppDescriptor> Apps { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

