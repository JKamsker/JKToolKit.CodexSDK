using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for reading app/connector metadata via <c>app/read</c>.
/// </summary>
public sealed class AppsReadOptions
{
    /// <summary>
    /// Gets or sets app identifiers to read. Upstream accepts at most 100 ids.
    /// </summary>
    public required IReadOnlyList<string> AppIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether display-only public tool summaries are included.
    /// </summary>
    public bool IncludeTools { get; set; }
}

/// <summary>
/// Result returned by <c>app/read</c>.
/// </summary>
public sealed record class AppsReadResult
{
    /// <summary>
    /// Gets app metadata returned by the server.
    /// </summary>
    public required IReadOnlyList<AppConnectorMetadata> Apps { get; init; }

    /// <summary>
    /// Gets requested app ids that the server could not resolve.
    /// </summary>
    public required IReadOnlyList<string> MissingAppIds { get; init; }

    /// <summary>
    /// Gets the raw app/read payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Metadata for an app/connector returned by <c>app/read</c>.
/// </summary>
public sealed record class AppConnectorMetadata
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
    /// Gets an optional app description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional icon URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets an optional dark-mode icon URL.
    /// </summary>
    public string? IconUrlDark { get; init; }

    /// <summary>
    /// Gets an optional distribution channel string.
    /// </summary>
    public string? DistributionChannel { get; init; }

    /// <summary>
    /// Gets an optional install URL.
    /// </summary>
    public string? InstallUrl { get; init; }

    /// <summary>
    /// Gets plugin display names associated with the app.
    /// </summary>
    public required IReadOnlyList<string> PluginDisplayNames { get; init; }

    /// <summary>
    /// Gets optional display-only public tool summaries.
    /// </summary>
    public required IReadOnlyList<AppToolSummaryDescriptor> ToolSummaries { get; init; }

    /// <summary>
    /// Gets the raw app metadata payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Display-only public tool metadata returned by <c>app/read</c>.
/// </summary>
public sealed record class AppToolSummaryDescriptor
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional display title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the raw tool summary payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for reading the installed connector runtime snapshot via <c>app/installed</c>.
/// </summary>
public sealed class AppsInstalledOptions
{
    /// <summary>
    /// Gets or sets an optional loaded thread id used to evaluate effective app configuration.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the server should refresh hosted connector runtime tools first.
    /// </summary>
    public bool ForceRefresh { get; set; }
}

/// <summary>
/// Result returned by <c>app/installed</c>.
/// </summary>
public sealed record class AppsInstalledResult
{
    /// <summary>
    /// Gets installed connector runtime state entries.
    /// </summary>
    public required IReadOnlyList<InstalledAppDescriptor> Apps { get; init; }

    /// <summary>
    /// Gets the raw app/installed payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Installed connector runtime state returned by <c>app/installed</c>.
/// </summary>
public sealed record class InstalledAppDescriptor
{
    /// <summary>
    /// Gets the stable app identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the best-effort runtime name, when present.
    /// </summary>
    public string? RuntimeName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the app is effectively enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the app has at least one model-visible callable tool.
    /// </summary>
    public bool Callable { get; init; }

    /// <summary>
    /// Gets the raw installed app payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
