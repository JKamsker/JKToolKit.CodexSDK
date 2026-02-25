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

            if (string.IsNullOrWhiteSpace(settings.Name))
            {
                Console.Error.WriteLine("Specify --name <NAME>.");
                return 1;
            }

            // Some thread operations require the thread to be loaded into the current app-server process.
            var thread = await codex.ResumeThreadAsync(settings.ThreadId, ct);
            await codex.SetThreadNameAsync(thread.Id, settings.Name, ct);
            Console.WriteLine($"Set thread name: {thread.Id} -> {settings.Name}");
            return 0;
        });
}

public sealed class AppServerThreadSetNameSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";

    [CommandOption("--name <NAME>")]
    public string? Name { get; init; }
}
