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
    /// Gets or sets a value indicating whether to bypass caches and force a reload.
    /// </summary>
    public bool ForceReload { get; set; }
}

