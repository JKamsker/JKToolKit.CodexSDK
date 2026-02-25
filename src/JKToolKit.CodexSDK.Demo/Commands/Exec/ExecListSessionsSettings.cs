using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecListSessionsSettings : CommandSettings
{
    [CommandOption("-s|--sessions <DIR>")]
    public string? SessionsRoot { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--cwd <DIR>")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--id-like <PATTERN>")]
    public string? SessionIdPattern { get; init; }

    [CommandOption("--limit <N>")]
    public int Limit { get; init; } = 25;
}

