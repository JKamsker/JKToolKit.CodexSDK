using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Describes a marketplace loading error returned by plugin APIs.
/// </summary>
public sealed record class MarketplaceLoadError
{
    /// <summary>
    /// Gets the marketplace path that failed to load.
    /// </summary>
    public required string MarketplacePath { get; init; }

    /// <summary>
    /// Gets the load failure message.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Describes a plugin summary entry.
/// </summary>
public sealed record class PluginSummaryDescriptor
{
    /// <summary>
    /// Gets the stable plugin identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name reported by the marketplace.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the plugin is already installed.
    /// </summary>
    public bool Installed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the installed plugin is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the plugin auth policy when the marketplace provides one.
    /// </summary>
    public required string AuthPolicy { get; init; }

    /// <summary>
    /// Gets the typed plugin auth policy when the marketplace provides one.
    /// </summary>
    public required PluginAuthPolicy AuthPolicyValue { get; init; }

    /// <summary>
    /// Gets the plugin install policy when the marketplace provides one.
    /// </summary>
    public required string InstallPolicy { get; init; }

    /// <summary>
    /// Gets the typed plugin install policy when the marketplace provides one.
    /// </summary>
    public required PluginInstallPolicy InstallPolicyValue { get; init; }

    /// <summary>
    /// Gets typed plugin interface metadata when the marketplace provides it.
    /// </summary>
    public PluginInterfaceMetadata? Interface { get; init; }

    /// <summary>
    /// Gets the raw source payload when the marketplace provides one.
    /// </summary>
    public required JsonElement Source { get; init; }

    /// <summary>
    /// Gets typed source metadata when the marketplace provides it.
    /// </summary>
    public required PluginSourceDescriptor SourceInfo { get; init; }

    /// <summary>
    /// Gets the raw plugin summary payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes a plugin marketplace.
/// </summary>
public sealed record class PluginMarketplace
{
    /// <summary>
    /// Gets the marketplace display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the marketplace file-system path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets typed marketplace interface metadata when the payload provides it.
    /// </summary>
    public PluginMarketplaceInterfaceMetadata? Interface { get; init; }

    /// <summary>
    /// Gets the plugins exposed by the marketplace.
    /// </summary>
    public required IReadOnlyList<PluginSummaryDescriptor> Plugins { get; init; }

    /// <summary>
    /// Gets the raw marketplace payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes a plugin-related app summary.
/// </summary>
public sealed record class PluginAppDescriptor
{
    /// <summary>
    /// Gets the stable app identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the app display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the app needs auth after install.
    /// </summary>
    public bool NeedsAuth { get; init; }

    /// <summary>
    /// Gets the optional app description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional app install URL.
    /// </summary>
    public string? InstallUrl { get; init; }

    /// <summary>
    /// Gets the raw app payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes a plugin-related skill summary.
/// </summary>
public sealed record class PluginSkillDescriptor
{
    /// <summary>
    /// Gets the skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the skill path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets a value indicating whether the skill is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the optional skill description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional short skill description.
    /// </summary>
    public string? ShortDescription { get; init; }

    /// <summary>
    /// Gets typed skill interface metadata when the payload provides it.
    /// </summary>
    public PluginSkillInterfaceMetadata? Interface { get; init; }

    /// <summary>
    /// Gets the raw skill payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes a plugin detail payload.
/// </summary>
public sealed record class PluginDetailDescriptor
{
    /// <summary>
    /// Gets the summary payload for the plugin.
    /// </summary>
    public required PluginSummaryDescriptor Summary { get; init; }

    /// <summary>
    /// Gets the optional plugin description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the marketplace display name.
    /// </summary>
    public required string MarketplaceName { get; init; }

    /// <summary>
    /// Gets the marketplace path.
    /// </summary>
    public required string MarketplacePath { get; init; }

    /// <summary>
    /// Gets the MCP servers advertised by the plugin.
    /// </summary>
    public required IReadOnlyList<string> McpServers { get; init; }

    /// <summary>
    /// Gets the skills exposed by the plugin.
    /// </summary>
    public required IReadOnlyList<PluginSkillDescriptor> Skills { get; init; }

    /// <summary>
    /// Gets the apps exposed by the plugin.
    /// </summary>
    public required IReadOnlyList<PluginAppDescriptor> Apps { get; init; }

    /// <summary>
    /// Gets the raw plugin detail payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
