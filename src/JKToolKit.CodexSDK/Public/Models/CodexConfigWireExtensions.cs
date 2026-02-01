namespace JKToolKit.CodexSDK.Public.Models;

public static class CodexConfigWireExtensions
{
    public static string ToMcpWireValue(this CodexApprovalPolicy policy) => policy.Value;

    public static string ToMcpWireValue(this CodexSandboxMode mode) => mode.Value;

    public static string ToAppServerWireValue(this CodexSandboxMode mode) => mode.Value;
}

