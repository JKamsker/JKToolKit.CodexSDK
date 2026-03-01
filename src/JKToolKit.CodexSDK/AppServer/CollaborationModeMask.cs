using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a collaboration mode preset exposed by the app-server (experimental).
/// </summary>
public sealed record class CollaborationModeMask
{
    /// <summary>
    /// Gets the preset name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the mode kind (wire value), when present.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    /// Gets the model identifier, when present.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the reasoning effort (wire value), when present.
    /// </summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the mask.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

