using JKToolKit.CodexSDK.Demo.Commands.Common;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public abstract class AppServerThreadsSettingsBase : RepoCodexRuntimeWithTimeoutSettingsBase
{
    [CommandOption("--experimental-api")]
    public bool ExperimentalApi { get; init; }

    [CommandOption("--json")]
    public bool Json { get; init; }
}

