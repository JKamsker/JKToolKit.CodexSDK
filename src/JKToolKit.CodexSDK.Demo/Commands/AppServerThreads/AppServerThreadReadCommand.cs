using Spectre.Console;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

public sealed class AppServerThreadReadCommand : AsyncCommand<AppServerThreadReadSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerThreadReadSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.ThreadId))
            {
                Console.Error.WriteLine("Specify --thread <ID>.");
                return 1;
            }

            var res = await codex.ReadThreadAsync(settings.ThreadId, ct);

            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(res.Raw);
                return 0;
            }

            AnsiConsole.MarkupLine($"[bold]Thread:[/] {Markup.Escape(res.Thread.ThreadId)}");
            if (!string.IsNullOrWhiteSpace(res.Thread.Name))
            {
                AnsiConsole.MarkupLine($"[dim]Name:[/] {Markup.Escape(res.Thread.Name)}");
            }

            if (res.Thread.Archived is not null)
            {
                AnsiConsole.MarkupLine($"[dim]Archived:[/] {Markup.Escape(res.Thread.Archived.Value.ToString())}");
            }

            if (res.Thread.CreatedAt is not null)
            {
                AnsiConsole.MarkupLine($"[dim]CreatedAt:[/] {Markup.Escape(res.Thread.CreatedAt.Value.ToString("u"))}");
            }

            if (!string.IsNullOrWhiteSpace(res.Thread.Cwd))
            {
                AnsiConsole.MarkupLine($"[dim]Cwd:[/] {Markup.Escape(res.Thread.Cwd)}");
            }

            if (!string.IsNullOrWhiteSpace(res.Thread.Model))
            {
                AnsiConsole.MarkupLine($"[dim]Model:[/] {Markup.Escape(res.Thread.Model)}");
            }

            return 0;
        });
}

public sealed class AppServerThreadReadSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--thread <ID>")]
    public string ThreadId { get; init; } = "";
}
