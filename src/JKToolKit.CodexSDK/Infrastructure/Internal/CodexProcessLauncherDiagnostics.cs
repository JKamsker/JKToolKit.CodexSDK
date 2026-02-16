namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexProcessLauncherDiagnostics
{
    internal static string CreateDiagnosticMessage(string? stderr, string executablePath) =>
        string.IsNullOrWhiteSpace(stderr)
            ? $"Failed to start Codex CLI process using executable '{executablePath}'. See inner exception for details."
            : $"Failed to start Codex CLI process using executable '{executablePath}'. Stderr: {stderr}";
}

