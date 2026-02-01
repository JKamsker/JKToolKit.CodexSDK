using Xunit;

namespace JKToolKit.CodexSDK.Tests.TestHelpers;

public sealed class CodexE2EFactAttribute : FactAttribute
{
    public CodexE2EFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CODEX_E2E"), "1", StringComparison.Ordinal))
        {
            Skip = "Set CODEX_E2E=1 to enable Codex E2E tests.";
        }
    }
}

