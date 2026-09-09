using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents managed application requirements surfaced by <c>configRequirements/read</c>.
/// </summary>
public sealed record class ApplicationRequirements
{
    /// <summary>
    /// Gets application traffic network requirements, when present.
    /// </summary>
    public ApplicationNetworkRequirements? Network { get; init; }

    /// <summary>
    /// Gets the raw JSON application requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents managed application network requirements.
/// </summary>
public sealed record class ApplicationNetworkRequirements
{
    /// <summary>
    /// Gets whether application network policy is enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets exact-domain permissions for application traffic.
    /// </summary>
    public IReadOnlyDictionary<string, NetworkDomainPermission>? Domains { get; init; }

    /// <summary>
    /// Gets the raw JSON application network requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
