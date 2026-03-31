using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>plugin/list</c>.
/// </summary>
public sealed class PluginListOptions
{
    /// <summary>
    /// Gets or sets the absolute working directories used to resolve plugin marketplaces.
    /// </summary>
    public IReadOnlyList<string>? Cwds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether remote marketplace sync should be forced.
    /// </summary>
    public bool? ForceRemoteSync { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/list</c>.
/// </summary>
public sealed record class PluginListResult
{
    /// <summary>
    /// Gets the marketplaces returned by the list request.
    /// </summary>
    public required IReadOnlyList<PluginMarketplace> Marketplaces { get; init; }

    /// <summary>
    /// Gets the featured plugin identifiers.
    /// </summary>
    public required IReadOnlyList<string> FeaturedPluginIds { get; init; }

    /// <summary>
    /// Gets marketplace load errors returned by the server.
    /// </summary>
    public required IReadOnlyList<MarketplaceLoadError> MarketplaceLoadErrors { get; init; }

    /// <summary>
    /// Gets the remote sync error when the server reports one.
    /// </summary>
    public string? RemoteSyncError { get; init; }

    /// <summary>
    /// Gets the raw plugin list payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/read</c>.
/// </summary>
public sealed class PluginReadOptions
{
    /// <summary>
    /// Gets or sets the absolute marketplace path that contains the plugin.
    /// </summary>
    public required string MarketplacePath { get; set; }

    /// <summary>
    /// Gets or sets the plugin name within the marketplace.
    /// </summary>
    public required string PluginName { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/read</c>.
/// </summary>
public sealed record class PluginReadResult
{
    /// <summary>
    /// Gets the plugin detail payload.
    /// </summary>
    public required PluginDetailDescriptor Plugin { get; init; }

    /// <summary>
    /// Gets the raw plugin read payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/install</c>.
/// </summary>
public sealed class PluginInstallOptions
{
    /// <summary>
    /// Gets or sets the absolute marketplace path that contains the plugin.
    /// </summary>
    public required string MarketplacePath { get; set; }

    /// <summary>
    /// Gets or sets the plugin name within the marketplace.
    /// </summary>
    public required string PluginName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether remote marketplace sync should be forced.
    /// </summary>
    public bool? ForceRemoteSync { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/install</c>.
/// </summary>
public sealed record class PluginInstallResult
{
    /// <summary>
    /// Gets the apps that still need auth after install.
    /// </summary>
    public required IReadOnlyList<PluginAppDescriptor> AppsNeedingAuth { get; init; }

    /// <summary>
    /// Gets the auth policy returned by the install request.
    /// </summary>
    public string? AuthPolicy { get; init; }

    /// <summary>
    /// Gets the typed auth policy returned by the install request.
    /// </summary>
    public PluginAuthPolicy? AuthPolicyValue { get; init; }

    /// <summary>
    /// Gets the raw plugin install payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>plugin/uninstall</c>.
/// </summary>
public sealed class PluginUninstallOptions
{
    /// <summary>
    /// Gets or sets the installed plugin identifier.
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether remote marketplace sync should be forced.
    /// </summary>
    public bool? ForceRemoteSync { get; set; }
}

/// <summary>
/// Result returned by <c>plugin/uninstall</c>.
/// </summary>
public sealed record class PluginUninstallResult
{
    /// <summary>
    /// Gets the raw plugin uninstall payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
