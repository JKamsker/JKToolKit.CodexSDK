using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOptOutNotifications;

public sealed class AppServerOptOutNotificationsSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "Summarize this repository in three short sentences.";
}
