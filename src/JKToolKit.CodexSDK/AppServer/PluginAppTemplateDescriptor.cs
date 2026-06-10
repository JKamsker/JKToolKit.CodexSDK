using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a plugin app-template unavailable reason.
/// </summary>
public readonly record struct PluginAppTemplateUnavailableReason
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginAppTemplateUnavailableReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin app-template unavailable reason cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the not-configured-for-workspace reason.
    /// </summary>
    public static PluginAppTemplateUnavailableReason NotConfiguredForWorkspace => new("NOT_CONFIGURED_FOR_WORKSPACE");

    /// <summary>
    /// Gets the no-active-workspace reason.
    /// </summary>
    public static PluginAppTemplateUnavailableReason NoActiveWorkspace => new("NO_ACTIVE_WORKSPACE");

    /// <summary>
    /// Parses an unavailable reason from a wire value.
    /// </summary>
    public static PluginAppTemplateUnavailableReason Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse an unavailable reason from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginAppTemplateUnavailableReason reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = default;
            return false;
        }

        reason = new PluginAppTemplateUnavailableReason(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginAppTemplateUnavailableReason"/>.
    /// </summary>
    public static implicit operator PluginAppTemplateUnavailableReason(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginAppTemplateUnavailableReason"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginAppTemplateUnavailableReason reason) => reason.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Describes a plugin app template.
/// </summary>
public sealed record class PluginAppTemplateDescriptor
{
    /// <summary>
    /// Gets the stable template id.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the template display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional template description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional canonical connector id.
    /// </summary>
    public string? CanonicalConnectorId { get; init; }

    /// <summary>
    /// Gets the optional light-mode logo URL.
    /// </summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// Gets the optional dark-mode logo URL.
    /// </summary>
    public string? LogoUrlDark { get; init; }

    /// <summary>
    /// Gets materialized app ids for this template.
    /// </summary>
    public required IReadOnlyList<string> MaterializedAppIds { get; init; }

    /// <summary>
    /// Gets the unavailable reason wire value, when present.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the typed unavailable reason, when present.
    /// </summary>
    public PluginAppTemplateUnavailableReason? ReasonValue { get; init; }

    /// <summary>
    /// Gets the raw app-template payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
