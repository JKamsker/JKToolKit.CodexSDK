using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of writing a remote skill via the app-server.
/// </summary>
public sealed record class RemoteSkillWriteResult
{
    /// <summary>
    /// Gets the skill identifier, when present.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the skill name, when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the skill path, when present.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

