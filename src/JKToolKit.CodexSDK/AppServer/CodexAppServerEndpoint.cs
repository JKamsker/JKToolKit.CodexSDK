using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Describes how <see cref="CodexAppServerClient"/> should reach a Codex app-server.
/// </summary>
public abstract record CodexAppServerEndpoint;

/// <summary>
/// Starts or attaches to a Codex app-server through a stdio process launch.
/// </summary>
public sealed record CodexAppServerStdioEndpoint : CodexAppServerEndpoint
{
    /// <summary>
    /// Initializes a new stdio endpoint.
    /// </summary>
    /// <param name="launch">The process launch configuration.</param>
    public CodexAppServerStdioEndpoint(CodexLaunch launch)
    {
        Launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    /// <summary>
    /// Gets the process launch configuration.
    /// </summary>
    public CodexLaunch Launch { get; }
}

/// <summary>
/// Attaches to an already-listening Codex app-server WebSocket endpoint.
/// </summary>
public sealed record CodexAppServerWebSocketEndpoint : CodexAppServerEndpoint
{
    /// <summary>
    /// Initializes a new WebSocket endpoint.
    /// </summary>
    /// <param name="uri">The WebSocket URI, using ws:// or wss://.</param>
    /// <param name="bearerToken">Optional bearer token for the WebSocket handshake.</param>
    public CodexAppServerWebSocketEndpoint(Uri uri, string? bearerToken = null)
    {
        ValidateUri(uri);
        Uri = uri;
        BearerToken = bearerToken;
    }

    /// <summary>
    /// Gets the WebSocket URI.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// Gets an optional bearer token sent as an Authorization header.
    /// </summary>
    public string? BearerToken { get; }

    private static void ValidateUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("WebSocket app-server URI must be absolute.", nameof(uri));
        }

        if (uri.Scheme is not "ws" and not "wss")
        {
            throw new ArgumentException("WebSocket app-server URI must use ws:// or wss://.", nameof(uri));
        }
    }
}
