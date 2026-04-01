using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Merge behavior for config writes.
/// </summary>
public enum ConfigMergeStrategy
{
    /// <summary>
    /// Replace the target value.
    /// </summary>
    Replace = 0,

    /// <summary>
    /// Upsert into the target value.
    /// </summary>
    Upsert = 1
}

/// <summary>
/// Options for <c>config/value/write</c>.
/// </summary>
public sealed class ConfigValueWriteOptions
{
    /// <summary>
    /// Gets or sets the dotted config key path to update.
    /// </summary>
    public required string KeyPath { get; set; }

    /// <summary>
    /// Gets or sets the JSON value to write.
    /// </summary>
    public required JsonElement Value { get; set; }

    /// <summary>
    /// Gets or sets the merge strategy.
    /// </summary>
    public required ConfigMergeStrategy MergeStrategy { get; set; }

    /// <summary>
    /// Gets or sets an optional target config file path.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets an optional expected config version for optimistic concurrency.
    /// </summary>
    public string? ExpectedVersion { get; set; }
}

/// <summary>
/// A single edit inside <c>config/batchWrite</c>.
/// </summary>
public sealed class ConfigEditOperation
{
    /// <summary>
    /// Gets or sets the dotted config key path to update.
    /// </summary>
    public required string KeyPath { get; set; }

    /// <summary>
    /// Gets or sets the JSON value to write.
    /// </summary>
    public required JsonElement Value { get; set; }

    /// <summary>
    /// Gets or sets the merge strategy.
    /// </summary>
    public required ConfigMergeStrategy MergeStrategy { get; set; }
}

/// <summary>
/// Options for <c>config/batchWrite</c>.
/// </summary>
public sealed class ConfigBatchWriteOptions
{
    /// <summary>
    /// Gets or sets the edits to apply atomically.
    /// </summary>
    public IReadOnlyList<ConfigEditOperation> Edits { get; set; } = Array.Empty<ConfigEditOperation>();

    /// <summary>
    /// Gets or sets an optional target config file path.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets an optional expected config version for optimistic concurrency.
    /// </summary>
    public string? ExpectedVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether loaded threads should hot-reload the updated user config.
    /// </summary>
    public bool ReloadUserConfig { get; set; }
}

/// <summary>
/// Status returned by config write operations.
/// </summary>
public enum ConfigWriteStatus
{
    /// <summary>
    /// The write succeeded without override warnings.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// The write succeeded but the resulting value is overridden by a higher-priority layer.
    /// </summary>
    OkOverridden = 1
}

/// <summary>
/// Additional override information returned when a config write is shadowed by another layer.
/// </summary>
public sealed record class ConfigWriteOverriddenMetadataInfo
{
    /// <summary>
    /// Gets the user-facing override message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets metadata for the layer that overrides the written value.
    /// </summary>
    public required ConfigLayerMetadataInfo OverridingLayer { get; init; }

    /// <summary>
    /// Gets the effective value after override resolution.
    /// </summary>
    public required JsonElement EffectiveValue { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for forward compatibility.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by config write operations.
/// </summary>
public sealed record class ConfigWriteResult
{
    /// <summary>
    /// Gets the write status.
    /// </summary>
    public required ConfigWriteStatus Status { get; init; }

    /// <summary>
    /// Gets the config version after the write.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the canonical file path that was written.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets override metadata when the written value is shadowed by another layer.
    /// </summary>
    public ConfigWriteOverriddenMetadataInfo? OverriddenMetadata { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
