namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for attaching to a Codex app-server over WebSocket.
/// </summary>
public sealed class CodexAppServerWebSocketOptions
{
    /// <summary>
    /// Gets or sets the WebSocket URI, using ws:// or wss://.
    /// </summary>
    public Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets an optional bearer token sent as an Authorization header.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Gets or sets the shared app-server client options used after the WebSocket connects.
    /// </summary>
    public CodexAppServerClientOptions ClientOptions { get; set; } = new();
}
