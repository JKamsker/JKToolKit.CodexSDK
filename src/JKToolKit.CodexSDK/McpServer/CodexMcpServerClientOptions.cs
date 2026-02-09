using System.Text.Json;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Options for launching and configuring a <see cref="CodexMcpServerClient"/>.
/// </summary>
public sealed class CodexMcpServerClientOptions
{
    /// <summary>
    /// Gets or sets the process launch configuration for the Codex executable.
    /// </summary>
    public CodexLaunch Launch { get; set; } = CodexLaunch.CodexOnPath().WithArgs("mcp-server");

    /// <summary>
    /// Gets or sets an optional explicit path to the Codex executable.
    /// </summary>
    public string? CodexExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the Codex home directory (passed as <c>CODEX_HOME</c> to the launched process).
    /// </summary>
    public string? CodexHomeDirectory { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the MCP server startup handshake.
    /// </summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout used when shutting down the MCP server process.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets an optional override for JSON serialization options used by the client.
    /// </summary>
    public JsonSerializerOptions? SerializerOptionsOverride { get; set; }

    /// <summary>
    /// Gets or sets the size of the internal buffer used for JSON-RPC notifications.
    /// </summary>
    public int NotificationBufferCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the client identity sent during MCP initialization.
    /// </summary>
    public McpClientInfo ClientInfo { get; set; } = new("ncodexsdk", "JKToolKit.CodexSDK", "1.0.0");

    /// <summary>
    /// Gets or sets an optional handler for server-initiated elicitation requests.
    /// </summary>
    public IMcpElicitationHandler? ElicitationHandler { get; set; }
}

