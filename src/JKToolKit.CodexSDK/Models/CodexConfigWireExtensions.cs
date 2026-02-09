namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Helper methods for converting SDK value objects to wire values used by Codex.
/// </summary>
public static class CodexConfigWireExtensions
{
    /// <summary>
    /// Converts an approval policy to its MCP wire value.
    /// </summary>
    public static string ToMcpWireValue(this CodexApprovalPolicy policy) => policy.Value;

    /// <summary>
    /// Converts a sandbox mode to its MCP wire value.
    /// </summary>
    public static string ToMcpWireValue(this CodexSandboxMode mode) => mode.Value;

    /// <summary>
    /// Converts a sandbox mode to its app-server wire value.
    /// </summary>
    public static string ToAppServerWireValue(this CodexSandboxMode mode) => mode.Value;
}

