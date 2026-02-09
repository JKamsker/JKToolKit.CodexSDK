using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the response payload from the <c>initialize</c> request.
/// </summary>
public sealed record AppServerInitializeResult
{
    /// <summary>
    /// Gets the raw JSON result payload.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Gets the server-provided user agent string, if present.
    /// </summary>
    public string? UserAgent { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppServerInitializeResult"/> from the raw JSON payload.
    /// </summary>
    public AppServerInitializeResult(JsonElement raw)
    {
        Raw = raw;

        UserAgent = raw.ValueKind == JsonValueKind.Object &&
                    raw.TryGetProperty("userAgent", out var ua) &&
                    ua.ValueKind == JsonValueKind.String
            ? ua.GetString()
            : null;
    }
}

