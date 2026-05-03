using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.SemanticKernelFunctionCalling;

public sealed class SemanticKernelFunctionCallingSettings : CommandSettings
{
    [CommandOption("--repo <PATH>")]
    public string? RepoPath { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }

    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--approval-policy <POLICY>")]
    public string? ApprovalPolicy { get; init; }

    [CommandOption("--sandbox <MODE>")]
    public string? Sandbox { get; init; }

    [CommandOption("--prompt <TEXT>")]
    public string? Prompt { get; init; }
}
