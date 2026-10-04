namespace JKToolKit.CodexSDK.Tests.TestHelpers;

public sealed class CodexLegacyMcpE2EFactAttribute : FactAttribute
{
    public CodexLegacyMcpE2EFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CODEX_E2E"), "1", StringComparison.Ordinal))
        {
            Skip = "Set CODEX_E2E=1 to enable Codex E2E tests.";
        }
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_E2E_MCP_EXECUTABLE")))
        {
            Skip = "Codex 0.160.0 removed mcp-server. Set CODEX_E2E_MCP_EXECUTABLE to a legacy CLI that supports it.";
        }
    }
}
