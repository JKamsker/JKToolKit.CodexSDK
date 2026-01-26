namespace NCodexSDK.Public.Models;

public static class CodexConfigWireExtensions
{
    public static string ToMcpWireValue(this CodexApprovalPolicy policy) => policy.Value;

    public static string ToMcpWireValue(this CodexSandboxMode mode) => mode.Value;

    public static object ToAppServerWireValue(this CodexSandboxMode mode) =>
        new { type = mode.Value };
}

