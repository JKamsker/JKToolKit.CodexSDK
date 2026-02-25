using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecAttachSettings : CommandSettings
{
    [CommandOption("--log <PATH>")]
    public string? LogFilePath { get; init; }

    [CommandOption("--follow")]
    public bool Follow { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }
}

