using JKToolKit.CodexSDK.AppServer;
using Spectre.Console;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadListLoadedCommand : AsyncCommand<AppServerThreadListLoadedSettings>
{
    public override Task<int> ExecuteAsync(
        CommandContext context,
        AppServerThreadListLoadedSettings settings,
        CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            var page = await codex.ListLoadedThreadsAsync(new ThreadLoadedListOptions
            {
                Cursor = string.IsNullOrWhiteSpace(settings.Cursor) ? null : settings.Cursor,
                Limit = settings.Limit
            }, ct);

            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(page.Raw);
                return 0;
            }

            if (page.ThreadIds.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim](no loaded threads)[/]");
            }
            else
            {
                foreach (var id in page.ThreadIds)
                {
                    Console.WriteLine(id);
                }
            }

            if (!string.IsNullOrWhiteSpace(page.NextCursor))
            {
                AnsiConsole.MarkupLine($"\n[dim]NextCursor:[/] {Markup.Escape(page.NextCursor)}");
            }

            return 0;
        });
}

public sealed class AppServerThreadListLoadedSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--cursor <CURSOR>")]
    public string? Cursor { get; init; }

    [CommandOption("--limit <N>")]
    public int? Limit { get; init; }
}

