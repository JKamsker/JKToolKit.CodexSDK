using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of <c>collaborationMode/list</c> (experimental).
/// </summary>
public sealed record class CollaborationModeListResult
{
    /// <summary>
    /// Gets the returned preset masks.
    /// </summary>
    public required IReadOnlyList<CollaborationModeMask> Data { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

