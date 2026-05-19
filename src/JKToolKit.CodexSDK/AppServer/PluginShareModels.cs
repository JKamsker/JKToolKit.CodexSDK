using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Describes a target principal for plugin sharing.
/// </summary>
public sealed record class PluginShareTarget
{
    /// <summary>
    /// Gets the target principal type.
    /// </summary>
    public required PluginSharePrincipalType PrincipalType { get; init; }

    /// <summary>
    /// Gets the target principal identifier.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the target role.
    /// </summary>
    public required PluginShareTargetRole Role { get; init; }
}

/// <summary>
/// Describes a principal returned by plugin share APIs.
/// </summary>
public sealed record class PluginSharePrincipal
{
    /// <summary>
    /// Gets the principal type.
    /// </summary>
    public required PluginSharePrincipalType PrincipalType { get; init; }

    /// <summary>
    /// Gets the principal identifier.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the principal role.
    /// </summary>
    public required PluginSharePrincipalRole Role { get; init; }

    /// <summary>
    /// Gets the principal display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw principal payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes remote sharing context associated with a plugin summary.
/// </summary>
public sealed record class PluginShareContextDescriptor
{
    /// <summary>
    /// Gets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; init; }

    /// <summary>
    /// Gets the remote plugin version.
    /// </summary>
    public string? RemoteVersion { get; init; }

    /// <summary>
    /// Gets the remote share discoverability.
    /// </summary>
    public PluginShareDiscoverability? Discoverability { get; init; }

    /// <summary>
    /// Gets the share URL.
    /// </summary>
    public string? ShareUrl { get; init; }

    /// <summary>
    /// Gets the creator account user identifier.
    /// </summary>
    public string? CreatorAccountUserId { get; init; }

    /// <summary>
    /// Gets the creator display name.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    /// Gets the remote share principals.
    /// </summary>
    public IReadOnlyList<PluginSharePrincipal>? SharePrincipals { get; init; }

    /// <summary>
    /// Gets the raw share context payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
