using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.McpServer;

public sealed class CodexMcpStartOptions
{
    public required string Prompt { get; set; }

    public CodexApprovalPolicy? ApprovalPolicy { get; set; }
    public CodexSandboxMode? Sandbox { get; set; }
    public string? Cwd { get; set; }
    public CodexModel? Model { get; set; }

    public bool? IncludePlanTool { get; set; }
}

