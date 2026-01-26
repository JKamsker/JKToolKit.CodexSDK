using NCodexSDK.Public.Models;

namespace NCodexSDK.AppServer;

public sealed class ThreadStartOptions
{
    public CodexModel? Model { get; set; }
    public string? Cwd { get; set; }

    public CodexApprovalPolicy? ApprovalPolicy { get; set; }
    public CodexSandboxMode? Sandbox { get; set; }
}

