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

            await codex.CleanThreadBackgroundTerminalsAsync(settings.ThreadId, ct);
            Console.WriteLine($"Cleaned background terminals for thread: {settings.ThreadId}");
            return 0;
        });
}

public sealed class AppServerThreadCleanBackgroundTerminalsSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
