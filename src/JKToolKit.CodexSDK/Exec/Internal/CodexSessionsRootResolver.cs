using JKToolKit.CodexSDK.Abstractions;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexSessionsRootResolver
{
    private const string CodexHomeEnvVar = "CODEX_HOME";

    internal static string GetEffectiveSessionsRootDirectory(CodexClientOptions clientOptions, ICodexPathProvider pathProvider)
    {
        var overrideDirectory = clientOptions.SessionsRootDirectory;
        if (string.IsNullOrWhiteSpace(overrideDirectory))
        {
            var home =
                clientOptions.CodexHomeDirectory ??
                Environment.GetEnvironmentVariable(CodexHomeEnvVar);

            if (!string.IsNullOrWhiteSpace(home))
            {
                overrideDirectory = Path.Combine(home, "sessions");
            }
        }

        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            Directory.CreateDirectory(overrideDirectory);
        }

        var effective = pathProvider.GetSessionsRootDirectory(overrideDirectory);
        if (!string.IsNullOrWhiteSpace(effective))
        {
            Directory.CreateDirectory(effective);
        }

        return effective;
    }
}

