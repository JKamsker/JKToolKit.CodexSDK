using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerSkillsApps;

public sealed class AppServerSkillsAppsCommand : AsyncCommand<AppServerSkillsAppsSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerSkillsAppsSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

            var skills = await codex.ListSkillsAsync(new SkillsListOptions
            {
                Cwd = repoPath,
                ForceReload = settings.ForceReload
            }, ct);

            var apps = await codex.ListAppsAsync(new AppsListOptions
            {
                Limit = settings.Limit > 0 ? settings.Limit : null,
                ForceRefetch = settings.ForceReload
            }, ct);

            if (settings.Json)
            {
                Console.WriteLine("skills/list:");
                AppServerThreadCommandHelpers.PrintJson(skills.Raw);
                Console.WriteLine("\napp/list:");
                AppServerThreadCommandHelpers.PrintJson(apps.Raw);
                return 0;
            }

            Console.WriteLine($"Skills ({skills.Skills.Count}):");
            foreach (var s in skills.Skills.Take(Math.Max(0, settings.Limit)))
            {
                var enabled = s.Enabled is null ? "" : $" enabled={s.Enabled}";
                Console.WriteLine($"- {s.Name}{enabled}");
            }

            Console.WriteLine();
            Console.WriteLine($"Apps ({apps.Apps.Count}):");
            foreach (var a in apps.Apps.Take(Math.Max(0, settings.Limit)))
            {
                var name = a.Title ?? a.Name ?? a.Id ?? "<unknown>";
                var enabled = a.IsEnabled is null ? "" : $" enabled={a.IsEnabled}";
                Console.WriteLine($"- {name}{enabled}");
            }

            if (!string.IsNullOrWhiteSpace(apps.NextCursor))
            {
                Console.WriteLine($"\nNextCursor: {apps.NextCursor}");
            }

            return 0;
        });
}

public sealed class AppServerSkillsAppsSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--limit <N>")]
    public int Limit { get; init; } = 25;

    [CommandOption("--force-reload")]
    public bool ForceReload { get; init; }
}

