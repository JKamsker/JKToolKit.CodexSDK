using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed descriptor for a remote skill returned by the app-server.
/// </summary>
public sealed record class RemoteSkillDescriptor
{
    /// <summary>
    /// Gets the remote skill identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the remote skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets an optional description, when present.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the remote skill descriptor.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

