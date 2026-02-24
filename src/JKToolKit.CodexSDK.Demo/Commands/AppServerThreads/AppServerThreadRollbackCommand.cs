using JKToolKit.CodexSDK.AppServer;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadRollbackCommand : AsyncCommand<AppServerThreadRollbackSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadRollbackSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            if (settings.NumTurns <= 0)
            {
                Console.Error.WriteLine("Specify --num-turns <N> where N > 0.");
                return 1;
            }

            // Some thread operations require the thread to be loaded into the current app-server process.
            var loaded = await codex.ResumeThreadAsync(settings.ThreadId, ct);
            var thread = await codex.RollbackThreadAsync(loaded.Id, settings.NumTurns, ct);
            Console.WriteLine($"Rolled back thread: {thread.Id}");
            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadRollbackSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";

    [CommandOption("--num-turns <N>")]
    public int NumTurns { get; init; } = 1;
}
