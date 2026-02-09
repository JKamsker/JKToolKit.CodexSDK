using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerApproval;

public sealed class AppServerApprovalSettings : CommandSettings
{
    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }

    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }
}
