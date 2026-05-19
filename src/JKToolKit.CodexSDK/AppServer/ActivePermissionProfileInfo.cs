using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Describes the active permission profile returned by thread lifecycle responses.
/// </summary>
public sealed record class ActivePermissionProfileInfo
{
    /// <summary>
    /// Gets the active profile identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the parent profile identifier, when upstream returns one.
    /// </summary>
    public string? Extends { get; init; }

    /// <summary>
    /// Gets the raw profile payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
