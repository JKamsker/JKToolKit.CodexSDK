using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public abstract class AppServerWithPromptSettingsBase : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "";
}

