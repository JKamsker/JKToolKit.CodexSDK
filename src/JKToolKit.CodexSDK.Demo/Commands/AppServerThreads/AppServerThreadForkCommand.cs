using JKToolKit.CodexSDK.AppServer;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadForkCommand : AsyncCommand<AppServerThreadForkSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadForkSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId) && string.IsNullOrWhiteSpace(settings.Path))
            {
                Console.Error.WriteLine("Specify --thread or --path.");
                return 1;
            }

            var thread = await codex.ForkThreadAsync(new ThreadForkOptions
            {
                ThreadId = string.IsNullOrWhiteSpace(settings.ThreadId) ? null : settings.ThreadId,
                Path = string.IsNullOrWhiteSpace(settings.Path) ? null : settings.Path
            }, ct);

            Console.WriteLine($"Forked thread: {thread.Id}");
            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(thread.Raw);
            }
            return 0;
        });
}

public sealed class AppServerThreadForkSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string? ThreadId { get; init; }

    [CommandOption("--path <PATH>")]
    public string? Path { get; init; }
}

