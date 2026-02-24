using JKToolKit.CodexSDK.AppServer;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadUnarchiveCommand : AsyncCommand<AppServerThreadUnarchiveSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadUnarchiveSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            var thread = await codex.UnarchiveThreadAsync(settings.ThreadId, ct);
            Console.WriteLine($"Unarchived thread: {thread.Id}");
            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadUnarchiveSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
