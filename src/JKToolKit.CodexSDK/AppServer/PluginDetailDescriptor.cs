using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

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
    public string? MarketplacePath { get; init; }

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
    /// Gets app templates exposed by the plugin.
    /// </summary>
    public required IReadOnlyList<PluginAppTemplateDescriptor> AppTemplates { get; init; }

    /// <summary>
    /// Gets the hooks exposed by the plugin.
    /// </summary>
    public required IReadOnlyList<PluginHookDescriptor> Hooks { get; init; }

    /// <summary>
    /// Gets the raw plugin detail payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
