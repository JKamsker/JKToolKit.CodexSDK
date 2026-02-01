using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.AppServer;

public sealed class ThreadStartOptions
{
    public CodexModel? Model { get; set; }
    public string? Cwd { get; set; }

    public CodexApprovalPolicy? ApprovalPolicy { get; set; }
    public CodexSandboxMode? Sandbox { get; set; }
}

