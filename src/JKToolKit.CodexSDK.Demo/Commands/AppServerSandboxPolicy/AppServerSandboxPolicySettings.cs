using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerSandboxPolicy;

public sealed class AppServerSandboxPolicySettings : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "";
}
