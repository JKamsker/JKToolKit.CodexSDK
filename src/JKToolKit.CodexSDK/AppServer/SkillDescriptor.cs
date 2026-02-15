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
    /// Gets an optional short description, when present.
    /// </summary>
    public string? ShortDescription { get; init; }

    /// <summary>
    /// Gets an optional path or identifier, when present.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets a value indicating whether the skill is enabled, when present.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets the working directory scope this skill was listed under, when present.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional skill scope string, when present.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets optional dependency metadata, when present (raw).
    /// </summary>
    public JsonElement? Dependencies { get; init; }

    /// <summary>
    /// Gets optional interface metadata, when present (raw).
    /// </summary>
    public JsonElement? Interface { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the skill descriptor.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

