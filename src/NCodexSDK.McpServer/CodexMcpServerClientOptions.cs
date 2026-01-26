using System.Text.Json;
using NCodexSDK.Public;

namespace NCodexSDK.McpServer;

public sealed class CodexMcpServerClientOptions
{
    public CodexLaunch Launch { get; set; } = CodexLaunch.CodexOnPath().WithArgs("mcp-server");

    public string? CodexExecutablePath { get; set; }

    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public JsonSerializerOptions? SerializerOptionsOverride { get; set; }

    public int NotificationBufferCapacity { get; set; } = 1000;

    public McpClientInfo ClientInfo { get; set; } = new("ncodexsdk", "NCodexSDK", "1.0.0");

    public IMcpElicitationHandler? ElicitationHandler { get; set; }
}

