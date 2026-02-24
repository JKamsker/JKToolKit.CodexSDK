using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadCompactCommand : AsyncCommand<AppServerThreadCompactSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadCompactSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            // Some thread operations require the thread to be loaded into the current app-server process.
            var thread = await codex.ResumeThreadAsync(settings.ThreadId, ct);
            await codex.CompactThreadAsync(thread.Id, ct);
            Console.WriteLine($"Compacted thread: {thread.Id}");
            return 0;
        });
}

public sealed class AppServerThreadCompactSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
