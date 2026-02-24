using JKToolKit.CodexSDK.AppServer;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadArchiveCommand : AsyncCommand<AppServerThreadArchiveSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadArchiveSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            var thread = await codex.ArchiveThreadAsync(settings.ThreadId, ct);
            Console.WriteLine($"Archived thread: {thread.Id}");
            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadArchiveSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
