using System.Text.Json;

#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>experimentalFeature/list</c>.
/// </summary>
public sealed class ExperimentalFeatureListOptions
{
    /// <summary>
    /// Gets or sets the opaque pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets the optional page size.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// A single experimental feature entry.
/// </summary>
public sealed record class ExperimentalFeatureListEntry
{
    public required string Name { get; init; }
    public string? Stage { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Announcement { get; init; }
    public bool Enabled { get; init; }
    public bool DefaultEnabled { get; init; }
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>experimentalFeature/list</c>.
/// </summary>
public sealed record class ExperimentalFeatureListResult
{
    public required IReadOnlyList<ExperimentalFeatureListEntry> Data { get; init; }
    public string? NextCursor { get; init; }
    public required JsonElement Raw { get; init; }
}

#pragma warning restore CS1591
