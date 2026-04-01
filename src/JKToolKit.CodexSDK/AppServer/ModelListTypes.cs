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
    public bool Hidden { get; init; }
    public bool IsDefault { get; init; }
    public bool SupportsPersonality { get; init; }
    public string? Upgrade { get; init; }
    public string? DefaultReasoningEffort { get; init; }
    public string? AvailabilityNuxMessage { get; init; }
    public IReadOnlyList<string> InputModalities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModelReasoningEffortOption> SupportedReasoningEfforts { get; init; } = Array.Empty<ModelReasoningEffortOption>();
    public ModelUpgradeInfo? UpgradeInfo { get; init; }
    public required JsonElement Raw { get; init; }
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
