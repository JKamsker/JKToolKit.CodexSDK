using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Review;

public sealed class ReviewSettings : CommandSettings
{
    [CommandOption("-C|--cd <DIR>")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--commit <SHA>")]
    public string? CommitSha { get; init; }

    [CommandOption("--base <BRANCH>")]
    public string? BaseBranch { get; init; }

    [CommandOption("--uncommitted")]
    public bool Uncommitted { get; init; }

    [CommandOption("--title <TITLE>")]
    public string? Title { get; init; }

    [CommandOption("--prompt <PROMPT>")]
    public string? PromptOption { get; init; }

    [CommandArgument(0, "[PROMPT]")]
    public string[] Prompt { get; init; } = [];

    [CommandOption("-c|--config <k=v>")]
    public string[] Config { get; init; } = [];

    [CommandOption("--enable <FEATURE>")]
    public string[] Enable { get; init; } = [];

    [CommandOption("--disable <FEATURE>")]
    public string[] Disable { get; init; } = [];

    [CommandOption("--additional <ARG>")]
    public string[] Additional { get; init; } = [];
}
