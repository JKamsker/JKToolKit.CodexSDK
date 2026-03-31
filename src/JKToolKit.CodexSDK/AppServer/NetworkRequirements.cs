using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents network requirements/proxy configuration constraints surfaced by <c>configRequirements/read</c>.
/// </summary>
public sealed record class NetworkRequirements
{
    /// <summary>
    /// Gets whether network access is enabled, when present.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets the HTTP proxy port, when present.
    /// </summary>
    public int? HttpPort { get; init; }

    /// <summary>
    /// Gets the SOCKS proxy port, when present.
    /// </summary>
    public int? SocksPort { get; init; }

    /// <summary>
    /// Gets whether upstream proxies are allowed, when present.
    /// </summary>
    public bool? AllowUpstreamProxy { get; init; }

    /// <summary>
    /// Gets whether non-loopback proxies are allowed, when present (dangerous).
    /// </summary>
    public bool? DangerouslyAllowNonLoopbackProxy { get; init; }

    /// <summary>
    /// Gets whether non-loopback admin access is allowed.
    /// </summary>
    /// <remarks>
    /// This legacy field is no longer projected from upstream requirements payloads.
    /// </remarks>
    [Obsolete("This legacy field is no longer projected from app-server network requirements.")]
    public bool? DangerouslyAllowNonLoopbackAdmin { get; init; }

    /// <summary>
    /// Gets whether all unix sockets are allowed, when present (dangerous).
    /// </summary>
    public bool? DangerouslyAllowAllUnixSockets { get; init; }

    /// <summary>
    /// Gets the canonical managed domain permission map, when present.
    /// </summary>
    public IReadOnlyDictionary<string, NetworkDomainPermission>? Domains { get; init; }

    /// <summary>
    /// Gets whether only managed allowed domains should be honored, when present.
    /// </summary>
    public bool? ManagedAllowedDomainsOnly { get; init; }

    /// <summary>
    /// Gets an allow-list of domains, when present.
    /// </summary>
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>
    /// Gets a deny-list of domains, when present.
    /// </summary>
    public IReadOnlyList<string>? DeniedDomains { get; init; }

    /// <summary>
    /// Gets an allow-list of unix socket paths, when present.
    /// </summary>
    public IReadOnlyList<string>? AllowUnixSockets { get; init; }

    /// <summary>
    /// Gets the canonical managed unix-socket permission map, when present.
    /// </summary>
    public IReadOnlyDictionary<string, NetworkUnixSocketPermission>? UnixSockets { get; init; }

    /// <summary>
    /// Gets whether local binding is allowed, when present.
    /// </summary>
    public bool? AllowLocalBinding { get; init; }

    /// <summary>
    /// Gets the raw JSON network requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
