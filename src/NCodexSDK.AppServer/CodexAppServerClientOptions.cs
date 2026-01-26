using System.Text.Json;
using NCodexSDK.Public;

namespace NCodexSDK.AppServer;

public sealed class CodexAppServerClientOptions
{
    public CodexLaunch Launch { get; set; } = CodexLaunch.CodexOnPath().WithArgs("app-server");

    public string? CodexExecutablePath { get; set; }

    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public AppServerClientInfo DefaultClientInfo { get; set; } = new(
        "ncodexsdk",
        "NCodexSDK",
        "1.0.0");

    public JsonSerializerOptions? SerializerOptionsOverride { get; set; }

    public int NotificationBufferCapacity { get; set; } = 5000;

    public IAppServerApprovalHandler? ApprovalHandler { get; set; }
}
