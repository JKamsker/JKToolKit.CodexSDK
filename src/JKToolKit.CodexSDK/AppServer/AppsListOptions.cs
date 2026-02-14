namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for listing apps/connectors via the app-server.
/// </summary>
public sealed class AppsListOptions
{
    /// <summary>
    /// Gets or sets an optional working directory scope, if supported upstream.
    /// </summary>
    public string? Cwd { get; set; }
}

