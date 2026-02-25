using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

public sealed class AppServerCollaborationModeSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "Say 'ok' and nothing else.";
}
