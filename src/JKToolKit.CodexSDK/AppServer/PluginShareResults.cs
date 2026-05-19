using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>plugin/share/save</c>.
/// </summary>
public sealed class PluginShareSaveOptions
{
    /// <summary>
    /// Gets or sets the absolute plugin directory path to save remotely.
    /// </summary>
    public required string PluginPath { get; set; }

    /// <summary>
    /// Gets or sets the remote plugin identifier when updating an existing share.
    /// </summary>
    public string? RemotePluginId { get; set; }

    /// <summary>
    /// Gets or sets the requested remote discoverability.
    /// </summary>
    public PluginShareDiscoverability? Discoverability { get; set; }

    /// <summary>
    /// Gets or sets the principals that should receive access.
    /// </summary>
    public IReadOnlyList<PluginShareTarget>? ShareTargets { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/share/save</c>.
/// </summary>
public sealed record class PluginShareSaveResult
{
    /// <summary>
    /// Gets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; init; }

    /// <summary>
    /// Gets the share URL.
    /// </summary>
    public required string ShareUrl { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/share/updateTargets</c>.
/// </summary>
public sealed class PluginShareUpdateTargetsOptions
{
    /// <summary>
    /// Gets or sets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; set; }

    /// <summary>
    /// Gets or sets the requested discoverability.
    /// </summary>
    public required PluginShareUpdateDiscoverability Discoverability { get; set; }

    /// <summary>
    /// Gets or sets the replacement share targets.
    /// </summary>
    public IReadOnlyList<PluginShareTarget>? ShareTargets { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/share/updateTargets</c>.
/// </summary>
public sealed record class PluginShareUpdateTargetsResult
{
    /// <summary>
    /// Gets the effective principals returned by the server.
    /// </summary>
    public required IReadOnlyList<PluginSharePrincipal> Principals { get; init; }

    /// <summary>
    /// Gets the effective discoverability.
    /// </summary>
    public required PluginShareDiscoverability Discoverability { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>plugin/share/list</c>.
/// </summary>
public sealed record class PluginShareListResult
{
    /// <summary>
    /// Gets shared plugin entries.
    /// </summary>
    public required IReadOnlyList<PluginShareListItem> Data { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes one <c>plugin/share/list</c> entry.
/// </summary>
public sealed record class PluginShareListItem
{
    /// <summary>
    /// Gets the shared plugin summary.
    /// </summary>
    public required PluginSummaryDescriptor Plugin { get; init; }

    /// <summary>
    /// Gets the local plugin path when the share is materialized locally.
    /// </summary>
    public string? LocalPluginPath { get; init; }

    /// <summary>
    /// Gets the raw list item payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/share/checkout</c>.
/// </summary>
public sealed class PluginShareCheckoutOptions
{
    /// <summary>
    /// Gets or sets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/share/checkout</c>.
/// </summary>
public sealed record class PluginShareCheckoutResult
{
    /// <summary>
    /// Gets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; init; }

    /// <summary>
    /// Gets the local plugin identifier.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets the plugin display name.
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// Gets the local plugin path.
    /// </summary>
    public required string PluginPath { get; init; }

    /// <summary>
    /// Gets the marketplace display name.
    /// </summary>
    public required string MarketplaceName { get; init; }

    /// <summary>
    /// Gets the marketplace path.
    /// </summary>
    public required string MarketplacePath { get; init; }

    /// <summary>
    /// Gets the remote plugin version.
    /// </summary>
    public string? RemoteVersion { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/share/delete</c>.
/// </summary>
public sealed class PluginShareDeleteOptions
{
    /// <summary>
    /// Gets or sets the remote plugin identifier.
    /// </summary>
    public required string RemotePluginId { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/share/delete</c>.
/// </summary>
public sealed record class PluginShareDeleteResult
{
    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
