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

            await codex.CompactThreadAsync(settings.ThreadId, ct);
            Console.WriteLine($"Compacted thread: {settings.ThreadId}");
            return 0;
        });
}

public sealed class AppServerThreadCompactSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
