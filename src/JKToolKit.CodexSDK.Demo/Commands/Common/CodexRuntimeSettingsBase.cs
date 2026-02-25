using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Common;

public abstract class CodexRuntimeSettingsBase : CommandSettings
{
    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }
}

public abstract class CodexRuntimeWithTimeoutSettingsBase : CodexRuntimeSettingsBase
{
    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }
}

public abstract class RepoCodexRuntimeWithTimeoutSettingsBase : CodexRuntimeWithTimeoutSettingsBase
{
    [CommandOption("--repo <PATH>")]
    public string? RepoPath { get; init; }
}

