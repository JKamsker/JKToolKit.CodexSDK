using System.Collections.Generic;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing skills via the app-server.
/// </summary>
public sealed class SkillsListOptions
{
    /// <summary>
    /// Gets or sets an optional working directory scope, if supported upstream.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets optional working directory scopes, if supported upstream.
    /// </summary>
    /// <remarks>
    /// When set, this takes precedence over <see cref="Cwd"/>.
    /// </remarks>
    public IReadOnlyList<string>? Cwds { get; set; }

    /// <summary>
    /// Gets or sets optional extra roots for resolving skills relative to <see cref="Cwd"/>, if supported upstream.
    /// </summary>
    /// <remarks>
    /// This option requires a single working directory scope. If <see cref="Cwds"/> is set, it must contain exactly one entry
    /// (or <see cref="Cwd"/> must be set) for this option to apply.
    /// </remarks>
    public IReadOnlyList<string>? ExtraRootsForCwd { get; set; }

    /// <summary>
    /// Gets or sets optional per-cwd extra roots to scan as user-scoped skills.
    /// </summary>
    /// <remarks>
    /// When set, each entry must specify the working directory scope and at least one extra root.
    /// </remarks>
    public IReadOnlyList<SkillsListExtraRootsForCwdEntry>? PerCwdExtraUserRoots { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass caches and force a reload.
    /// </summary>
    public bool ForceReload { get; set; }
}

/// <summary>
/// Represents an entry used by <see cref="SkillsListOptions.PerCwdExtraUserRoots"/>.
/// </summary>
public sealed record class SkillsListExtraRootsForCwdEntry
{
    /// <summary>
    /// Gets the working directory this entry applies to.
    /// </summary>
    public required string Cwd { get; init; }

    /// <summary>
    /// Gets the extra roots to scan for the given working directory.
    /// </summary>
    public required IReadOnlyList<string> ExtraUserRoots { get; init; }
}

