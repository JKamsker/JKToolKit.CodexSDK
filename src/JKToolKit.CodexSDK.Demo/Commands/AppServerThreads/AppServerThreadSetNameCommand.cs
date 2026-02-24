using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadSetNameCommand : AsyncCommand<AppServerThreadSetNameSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadSetNameSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            if (!settings.Clear && string.IsNullOrWhiteSpace(settings.Name))
            {
                Console.Error.WriteLine("Specify --name or pass --clear.");
                return 1;
            }

            var name = settings.Clear ? null : settings.Name;
            await codex.SetThreadNameAsync(settings.ThreadId, name, ct);
            Console.WriteLine(settings.Clear
                ? $"Cleared thread name: {settings.ThreadId}"
                : $"Set thread name: {settings.ThreadId} -> {settings.Name}");
            return 0;
        });
}

public sealed class AppServerThreadSetNameSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";

    [CommandOption("--name <NAME>")]
    public string? Name { get; init; }

    [CommandOption("--clear")]
    public bool Clear { get; init; }
}
