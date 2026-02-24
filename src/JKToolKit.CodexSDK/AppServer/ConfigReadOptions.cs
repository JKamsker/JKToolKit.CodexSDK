namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for reading the effective Codex configuration via the app-server (<c>config/read</c>).
/// </summary>
public sealed class ConfigReadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include the resolved config layer stack.
    /// </summary>
    public bool IncludeLayers { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory used to resolve project config layers.
    /// </summary>
    public string? Cwd { get; set; }
}

