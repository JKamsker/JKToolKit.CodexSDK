using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOverrides;

public sealed class AppServerOverridesSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "Summarize this repository in three short sentences.";

    [CommandOption("--print-limit <N>")]
    public int PrintLimit { get; init; } = 5;
}
