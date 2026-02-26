using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOutputSchema;

public sealed class AppServerOutputSchemaSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--prompt <TEXT>")]
    public string Prompt { get; init; } = "Return JSON only (no extra text), matching the schema. Example: {\"answer\":\"ok\"}.";
}
