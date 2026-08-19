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
    /// Gets or sets the marketplace kinds to include.
    /// </summary>
    public IReadOnlyList<PluginListMarketplaceKind>? MarketplaceKinds { get; set; }

    /// <summary>
    /// Gets or sets whether the server should bypass cached remote plugin catalog data.
    /// </summary>
    public bool ForceRefetch { get; set; }

    /// <summary>
    /// Gets or sets a legacy value indicating whether remote marketplace sync should be forced.
    /// </summary>
    /// <remarks>
    /// Codex 0.131 removed this request field; the SDK keeps the option for source compatibility and does not send it.
    /// </remarks>
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
    public string? MarketplacePath { get; set; }

    /// <summary>
    /// Gets or sets the remote marketplace name that contains the plugin.
    /// </summary>
    public string? RemoteMarketplaceName { get; set; }

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
    public string? MarketplacePath { get; set; }

    /// <summary>
    /// Gets or sets the remote marketplace name that contains the plugin.
    /// </summary>
    public string? RemoteMarketplaceName { get; set; }

    /// <summary>
    /// Gets or sets the plugin name within the marketplace.
    /// </summary>
    public required string PluginName { get; set; }

    /// <summary>
    /// Gets or sets a client-generated identifier used to correlate one installation attempt.
    /// </summary>
    public string? InstallAttemptId { get; set; }

    /// <summary>
    /// Gets or sets a legacy value indicating whether remote marketplace sync should be forced.
    /// </summary>
    /// <remarks>
    /// Codex 0.131 removed this request field; the SDK keeps the option for source compatibility and does not send it.
    /// </remarks>
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
    public required string AuthPolicy { get; init; }

    /// <summary>
    /// Gets the typed auth policy returned by the install request.
    /// </summary>
    public required PluginAuthPolicy AuthPolicyValue { get; init; }

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
    /// Gets or sets a legacy value indicating whether remote marketplace sync should be forced.
    /// </summary>
    /// <remarks>
    /// Codex 0.131 removed this request field; the SDK keeps the option for source compatibility and does not send it.
    /// </remarks>
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

/// <summary>
/// Represents the scope accepted by <c>plugin/search</c>.
/// </summary>
public readonly record struct PluginSearchScope
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginSearchScope(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin search scope cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Searches the global plugin catalog.
    /// </summary>
    public static PluginSearchScope Global => new("global");

    /// <summary>
    /// Searches workspace-visible plugins.
    /// </summary>
    public static PluginSearchScope Workspace => new("workspace");

    /// <summary>
    /// Searches user-owned plugins.
    /// </summary>
    public static PluginSearchScope Personal => new("personal");

    /// <summary>
    /// Parses a plugin search scope from a wire value.
    /// </summary>
    public static PluginSearchScope Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin search scope from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginSearchScope scope)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            scope = default;
            return false;
        }

        scope = new PluginSearchScope(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginSearchScope"/>.
    /// </summary>
    public static implicit operator PluginSearchScope(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginSearchScope"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginSearchScope scope) => scope.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Options for <c>plugin/search</c>.
/// </summary>
public sealed class PluginSearchOptions
{
    /// <summary>
    /// Gets or sets the required search term.
    /// </summary>
    public required string SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets an optional search scope.
    /// </summary>
    public PluginSearchScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets absolute working directories used to discover local marketplaces.
    /// </summary>
    public IReadOnlyList<string>? Cwds { get; set; }

    /// <summary>
    /// Gets or sets an optional cursor for paging remote results.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// Represents one <c>plugin/search</c> result.
/// </summary>
public sealed record class PluginSearchResult
{
    /// <summary>
    /// Gets the matched plugin summary.
    /// </summary>
    public required PluginSummaryDescriptor Plugin { get; init; }

    /// <summary>
    /// Gets the marketplace name that produced the result.
    /// </summary>
    public required string MarketplaceName { get; init; }

    /// <summary>
    /// Gets the marketplace path, when available.
    /// </summary>
    public string? MarketplacePath { get; init; }

    /// <summary>
    /// Gets the raw result payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a page returned by <c>plugin/search</c>.
/// </summary>
public sealed record class PluginSearchPage
{
    /// <summary>
    /// Gets the returned plugin search results.
    /// </summary>
    public required IReadOnlyList<PluginSearchResult> Data { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
