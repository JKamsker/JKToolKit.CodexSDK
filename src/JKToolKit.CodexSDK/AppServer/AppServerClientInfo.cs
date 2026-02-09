namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Identifies a client connecting to the Codex app server.
/// </summary>
public sealed record class AppServerClientInfo
{
    /// <summary>
    /// Gets the short client name (machine-readable).
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the client title (human-readable).
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Gets the client version string.
    /// </summary>
    public string Version { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppServerClientInfo"/>.
    /// </summary>
    public AppServerClientInfo(string name, string title, string version)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }
}

