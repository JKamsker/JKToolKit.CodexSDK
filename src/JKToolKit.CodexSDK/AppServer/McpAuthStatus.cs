namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents an MCP server auth status as reported by Codex.
/// </summary>
public enum McpAuthStatus
{
    /// <summary>
    /// The auth status is unknown or could not be parsed.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The MCP server does not support authentication, or Codex does not support auth for it.
    /// </summary>
    Unsupported = 1,

    /// <summary>
    /// The MCP server requires auth and the user is not logged in.
    /// </summary>
    NotLoggedIn = 2,

    /// <summary>
    /// The MCP server uses a bearer token provided via an environment variable.
    /// </summary>
    BearerToken = 3,

    /// <summary>
    /// The MCP server is authenticated via OAuth.
    /// </summary>
    OAuth = 4
}

