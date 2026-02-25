using JKToolKit.CodexSDK.Demo.Commands.Common;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecListSessionsSettings : CodexRuntimeSettingsBase
{
    [CommandOption("-s|--sessions <DIR>")]
    public string? SessionsRoot { get; init; }

    [CommandOption("--cwd <DIR>")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--id-like <PATTERN>")]
    public string? SessionIdPattern { get; init; }

    [CommandOption("--limit <N>")]
    public int Limit { get; init; } = 25;
}
