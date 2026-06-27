using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the acknowledgement returned by <c>thread/archive</c>.
/// </summary>
public sealed record class ThreadArchiveResult
{
    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the acknowledgement returned by <c>thread/delete</c>.
/// </summary>
public sealed record class ThreadDeleteResult
{
    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
