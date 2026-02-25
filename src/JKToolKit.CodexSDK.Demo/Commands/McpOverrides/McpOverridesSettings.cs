using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.McpOverrides;

public sealed class McpOverridesSettings : CommandSettings
{
    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <PATH>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }
}
