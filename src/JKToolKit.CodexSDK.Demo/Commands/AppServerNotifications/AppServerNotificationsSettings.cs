using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerNotifications;

public sealed class AppServerNotificationsSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--model <MODEL>")]
    public string? Model { get; init; }

    [CommandOption("--approval-policy <POLICY>")]
    public string? ApprovalPolicy { get; init; }

    [CommandOption("--sandbox <MODE>")]
    public string? Sandbox { get; init; }

    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "Say 'hello' and nothing else.";

    [CommandOption("--print-limit <N>")]
    public int PrintLimit { get; init; } = 10;
}

