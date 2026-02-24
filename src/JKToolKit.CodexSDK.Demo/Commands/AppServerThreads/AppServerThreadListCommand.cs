using JKToolKit.CodexSDK.AppServer;
using Spectre.Console;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadListCommand : AsyncCommand<AppServerThreadListSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadListSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            var page = await codex.ListThreadsAsync(new ThreadListOptions
            {
                Archived = settings.Archived,
                Cwd = string.IsNullOrWhiteSpace(settings.Cwd) ? null : settings.Cwd,
                Query = string.IsNullOrWhiteSpace(settings.Query) ? null : settings.Query,
                PageSize = settings.PageSize,
                Cursor = string.IsNullOrWhiteSpace(settings.Cursor) ? null : settings.Cursor,
                SortKey = string.IsNullOrWhiteSpace(settings.SortKey) ? null : settings.SortKey,
                SortDirection = string.IsNullOrWhiteSpace(settings.SortDirection) ? null : settings.SortDirection,
            }, ct);

            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(page.Raw);
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("ThreadId")
                .AddColumn("Name")
                .AddColumn("Archived")
                .AddColumn("CreatedAt")
                .AddColumn("Cwd")
                .AddColumn("Model");

            foreach (var t in page.Threads)
            {
                table.AddRow(
                    Markup.Escape(t.ThreadId),
                    Markup.Escape(t.Name ?? ""),
                    Markup.Escape(t.Archived?.ToString() ?? ""),
                    Markup.Escape(t.CreatedAt?.ToString("u") ?? ""),
                    Markup.Escape(t.Cwd ?? ""),
                    Markup.Escape(t.Model ?? ""));
            }

            AnsiConsole.Write(table);
            if (!string.IsNullOrWhiteSpace(page.NextCursor))
            {
                AnsiConsole.MarkupLine($"\n[dim]NextCursor:[/] {Markup.Escape(page.NextCursor)}");
            }

            return 0;
        });
}

public sealed class AppServerThreadListSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--archived <BOOL>")]
    public bool? Archived { get; init; }

    [CommandOption("--cwd <DIR>")]
    public string? Cwd { get; init; }

    [CommandOption("--query <QUERY>")]
    public string? Query { get; init; }

    [CommandOption("--page-size <N>")]
    public int? PageSize { get; init; }

    [CommandOption("--cursor <CURSOR>")]
    public string? Cursor { get; init; }

    [CommandOption("--sort-key <KEY>")]
    public string? SortKey { get; init; }

    [CommandOption("--sort-dir <DIR>")]
    public string? SortDirection { get; init; }
}

