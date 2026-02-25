using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.SdkReviewRoute;

public sealed class SdkReviewRouteSettings : CommandSettings
{
    [CommandOption("--repo <PATH>")]
    public string? RepoPath { get; init; }

    [CommandOption("--codex-path <PATH>")]
    public string? CodexExecutablePath { get; init; }

    [CommandOption("--codex-home <DIR>")]
    public string? CodexHomeDirectory { get; init; }

    [CommandOption("--timeout-seconds <SECONDS>")]
    public int? TimeoutSeconds { get; init; }
}
