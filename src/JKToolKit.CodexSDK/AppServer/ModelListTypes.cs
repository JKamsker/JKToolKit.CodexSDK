using System.Text.Json;

#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>model/list</c>.
/// </summary>
public sealed class ModelListOptions
{
    /// <summary>
    /// Gets or sets the opaque pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets the optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hidden models should be included.
    /// </summary>
    public bool? IncludeHidden { get; set; }
}

/// <summary>
/// A reasoning-effort option supported by a model.
/// </summary>
public sealed record class ModelReasoningEffortOption
{
    public required string ReasoningEffort { get; init; }
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Upgrade metadata advertised for a model.
/// </summary>
public sealed record class ModelUpgradeInfo
{
    public required string Model { get; init; }
    public string? UpgradeCopy { get; init; }
    public string? ModelLink { get; init; }
    public string? MigrationMarkdown { get; init; }
    public DateTimeOffset? RetirementAt { get; init; }
}

/// <summary>
/// A single entry returned by <c>model/list</c>.
/// </summary>
public sealed record class ModelListEntry
{
    public required string Id { get; init; }
    public required string Model { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ModelSpecialty { get; init; }
    public bool Hidden { get; init; }
    public bool IsDefault { get; init; }
    public bool SupportsPersonality { get; init; }
    public ModelMultiAgentVersion? MultiAgentVersion { get; init; }
    public string? Upgrade { get; init; }
    public required string DefaultReasoningEffort { get; init; }
    public string? AvailabilityNuxMessage { get; init; }
    public IReadOnlyList<string> InputModalities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModelReasoningEffortOption> SupportedReasoningEfforts { get; init; } = Array.Empty<ModelReasoningEffortOption>();
    public ModelUpgradeInfo? UpgradeInfo { get; init; }
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Multi-agent runtime advertised for a model.
/// </summary>
public readonly record struct ModelMultiAgentVersion
{
    private readonly string? _value;

    public string Value => _value ?? string.Empty;

    private ModelMultiAgentVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Model multi-agent version cannot be empty or whitespace.", nameof(value));

        _value = value;
    }

    public static ModelMultiAgentVersion Disabled => new("disabled");

    public static ModelMultiAgentVersion V1 => new("v1");

    public static ModelMultiAgentVersion V2 => new("v2");

    public static ModelMultiAgentVersion Parse(string value) => new(value);

    public static bool TryParse(string? value, out ModelMultiAgentVersion version)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            version = default;
            return false;
        }

        version = new ModelMultiAgentVersion(value);
        return true;
    }

    public static implicit operator ModelMultiAgentVersion(string value) => Parse(value);

    public static implicit operator string(ModelMultiAgentVersion version) => version.Value;

    public override string ToString() => Value;
}

/// <summary>
/// Result returned by <c>model/list</c>.
/// </summary>
public sealed record class ModelListResult
{
    public required IReadOnlyList<ModelListEntry> Data { get; init; }
    public string? NextCursor { get; init; }
    public required JsonElement Raw { get; init; }
}

#pragma warning restore CS1591
