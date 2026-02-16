using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.StructuredReview;

public sealed class StructuredReviewSettings : CommandSettings
{
    [CommandOption("-C|--cd <DIR>")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("-m|--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("-r|--reasoning <EFFORT>")]
    public string? Reasoning { get; init; }

    [CommandOption("--prompt <PROMPT>")]
    public string? PromptOption { get; init; }

    [CommandArgument(0, "[PROMPT]")]
    public string[] Prompt { get; init; } = [];

    [CommandOption("--max-attempts|--max-retries <N>")]
    public int MaxAttempts { get; init; } = 3;

    [CommandOption("--base <BRANCH>")]
    public string? BaseBranch { get; init; }

    [CommandOption("--commit <SHA>")]
    public string? CommitSha { get; init; }

    [CommandOption("--since <REF>")]
    public string? CommitsSince { get; init; }
}
