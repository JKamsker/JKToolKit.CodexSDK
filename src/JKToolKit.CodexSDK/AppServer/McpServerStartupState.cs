namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the lifecycle state of an MCP server startup attempt.
/// </summary>
public enum McpServerStartupState
{
    /// <summary>
    /// The MCP server is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// The MCP server started successfully and is ready.
    /// </summary>
    Ready,

    /// <summary>
    /// The MCP server startup failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The MCP server startup was cancelled.
    /// </summary>
    Cancelled
}
