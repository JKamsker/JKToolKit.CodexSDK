using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a best-effort typed descriptor for an app/connector returned by the app-server.
/// </summary>
public sealed record class AppDescriptor
{
    /// <summary>
    /// Gets the app identifier, when present.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the app name, when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets an optional description, when present.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional logo URL, when present.
    /// </summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// Gets an optional dark-mode logo URL, when present.
    /// </summary>
    public string? LogoUrlDark { get; init; }

    /// <summary>
    /// Gets an optional distribution channel string, when present.
    /// </summary>
    public string? DistributionChannel { get; init; }

    /// <summary>
    /// Gets an optional install URL, when present.
    /// </summary>
    public string? InstallUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the app is accessible, when present.
    /// </summary>
    public bool? IsAccessible { get; init; }

    /// <summary>
    /// Gets a value indicating whether the app is enabled, when present.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Gets the app title, when present.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional disabled reason string, when present.
    /// </summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the app descriptor.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

