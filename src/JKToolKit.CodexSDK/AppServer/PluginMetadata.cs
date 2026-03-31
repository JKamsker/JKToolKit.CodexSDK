using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Describes typed marketplace interface metadata reported by plugin APIs.
/// </summary>
public sealed record class PluginMarketplaceInterfaceMetadata
{
    /// <summary>
    /// Gets the marketplace display name exposed via the interface payload.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the raw marketplace interface payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes typed plugin interface metadata reported by plugin APIs.
/// </summary>
public sealed record class PluginInterfaceMetadata
{
    /// <summary>
    /// Gets the optional UI display name for the plugin.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the optional short plugin description from the interface payload.
    /// </summary>
    public string? ShortDescription { get; init; }

    /// <summary>
    /// Gets the optional long plugin description from the interface payload.
    /// </summary>
    public string? LongDescription { get; init; }

    /// <summary>
    /// Gets the optional plugin category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the optional developer name.
    /// </summary>
    public string? DeveloperName { get; init; }

    /// <summary>
    /// Gets the optional brand color.
    /// </summary>
    public string? BrandColor { get; init; }

    /// <summary>
    /// Gets the starter prompts advertised by the interface payload.
    /// </summary>
    public required IReadOnlyList<string> DefaultPrompts { get; init; }

    /// <summary>
    /// Gets the advertised capabilities.
    /// </summary>
    public required IReadOnlyList<string> Capabilities { get; init; }

    /// <summary>
    /// Gets the advertised screenshot URLs.
    /// </summary>
    public required IReadOnlyList<string> Screenshots { get; init; }

    /// <summary>
    /// Gets the optional privacy policy URL.
    /// </summary>
    public string? PrivacyPolicyUrl { get; init; }

    /// <summary>
    /// Gets the optional terms of service URL.
    /// </summary>
    public string? TermsOfServiceUrl { get; init; }

    /// <summary>
    /// Gets the optional website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Gets the optional composer icon payload.
    /// </summary>
    public JsonElement? ComposerIcon { get; init; }

    /// <summary>
    /// Gets the optional logo payload.
    /// </summary>
    public JsonElement? Logo { get; init; }

    /// <summary>
    /// Gets the raw plugin interface payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes typed skill interface metadata reported by plugin APIs.
/// </summary>
public sealed record class PluginSkillInterfaceMetadata
{
    /// <summary>
    /// Gets the optional UI display name for the skill.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the optional short skill description from the interface payload.
    /// </summary>
    public string? ShortDescription { get; init; }

    /// <summary>
    /// Gets the optional default prompt for the skill.
    /// </summary>
    public string? DefaultPrompt { get; init; }

    /// <summary>
    /// Gets the optional brand color.
    /// </summary>
    public string? BrandColor { get; init; }

    /// <summary>
    /// Gets the optional small icon URL.
    /// </summary>
    public string? IconSmall { get; init; }

    /// <summary>
    /// Gets the optional large icon URL.
    /// </summary>
    public string? IconLarge { get; init; }

    /// <summary>
    /// Gets the raw skill interface payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Describes typed plugin source metadata reported by plugin APIs.
/// </summary>
public sealed record class PluginSourceDescriptor
{
    /// <summary>
    /// Gets the typed source kind when the payload provides one.
    /// </summary>
    public PluginSourceType? Type { get; init; }

    /// <summary>
    /// Gets the raw source payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
