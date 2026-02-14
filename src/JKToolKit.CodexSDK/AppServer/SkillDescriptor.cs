using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed descriptor for a skill returned by the app-server.
/// </summary>
public sealed record class SkillDescriptor
{
    /// <summary>
    /// Gets the skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets an optional description, when present.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional path or identifier, when present.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the skill descriptor.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

