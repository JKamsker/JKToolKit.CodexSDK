using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecSettings : CommandSettings
{
    [CommandOption("-p|--prompt <PROMPT>")]
    public string? PromptOption { get; init; }

    [CommandArgument(0, "[PROMPT]")]
    public string[] Prompt { get; init; } = [];

    [CommandOption("-w|--workdir <DIR>")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("-s|--sessions <DIR>")]
    public string? SessionsRoot { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("-m|--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("-r|--reasoning <EFFORT>")]
    public string? Reasoning { get; init; }

    [CommandOption("--no-follow")]
    public bool NoFollow { get; init; }
}
