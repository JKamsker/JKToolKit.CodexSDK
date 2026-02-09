namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Identifies an MCP client during initialization.
/// </summary>
/// <param name="Name">Short client name (machine-readable).</param>
/// <param name="Title">Client title (human-readable).</param>
/// <param name="Version">Client version string.</param>
public sealed record McpClientInfo(string Name, string Title, string Version);

