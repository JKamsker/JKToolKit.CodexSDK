using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadCleanBackgroundTerminalsCommand : AsyncCommand<AppServerThreadCleanBackgroundTerminalsSettings>
{
    public override Task<int> ExecuteAsync(
        CommandContext context,
        AppServerThreadCleanBackgroundTerminalsSettings settings,
        CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            // Some thread operations require the thread to be loaded into the current app-server process.
            var thread = await codex.ResumeThreadAsync(settings.ThreadId, ct);
            await codex.CleanThreadBackgroundTerminalsAsync(thread.Id, ct);
            Console.WriteLine($"Cleaned background terminals for thread: {thread.Id}");
            return 0;
        });
}

public sealed class AppServerThreadCleanBackgroundTerminalsSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
