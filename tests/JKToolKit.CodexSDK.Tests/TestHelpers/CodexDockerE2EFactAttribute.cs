using Xunit;

namespace JKToolKit.CodexSDK.Tests.TestHelpers;

public sealed class CodexDockerE2EFactAttribute : FactAttribute
{
    public CodexDockerE2EFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CODEX_DOCKER_E2E"), "1", StringComparison.Ordinal))
        {
            Skip = "Set CODEX_DOCKER_E2E=1 to enable Docker remote app-server E2E tests.";
            return;
        }

        var codexHome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
        if (!File.Exists(Path.Combine(codexHome, "auth.json")) ||
            !File.Exists(Path.Combine(codexHome, "config.toml")))
        {
            Skip = "Docker remote app-server E2E tests require auth.json and config.toml in the local Codex home.";
        }
    }
}
