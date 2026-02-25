using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerConfig;

public sealed class AppServerConfigWriteCommand : AsyncCommand<AppServerConfigWriteSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerConfigWriteSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (string.IsNullOrWhiteSpace(settings.CodexHomeDirectory))
            {
                Console.Error.WriteLine("Refusing to write config without an explicit --codex-home <DIR> (use a temp copy to avoid modifying your real config).");
                return 1;
            }

            if (!settings.Apply)
            {
                Console.Error.WriteLine("Refusing to write config without --apply.");
                return 1;
            }

            var didAnything = false;

            if (!string.IsNullOrWhiteSpace(settings.RemoteSkillId) || !string.IsNullOrWhiteSpace(settings.RemoteSkillName))
            {
                didAnything = true;

                var remote = await codex.ReadRemoteSkillsAsync(ct);
                var match = remote.Skills.FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(settings.RemoteSkillId) && s.Id.Equals(settings.RemoteSkillId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(settings.RemoteSkillName) && s.Name.Equals(settings.RemoteSkillName, StringComparison.OrdinalIgnoreCase)));

                if (match is null)
                {
                    Console.Error.WriteLine("Remote skill not found. Use `appserver-config` to list available remote skills.");
                    return 1;
                }

                Console.WriteLine($"Writing remote skill: {match.Name} ({match.Id}) preload={settings.Preload}");
                var result = await codex.WriteRemoteSkillAsync(match.Id, settings.Preload, ct);
                Console.WriteLine($"Result: id={result.Id ?? "n/a"} name={result.Name ?? "n/a"} path={result.Path ?? "n/a"}");
            }

            if (settings.SkillsEnabled is { } enabled && !string.IsNullOrWhiteSpace(settings.SkillsPath))
            {
                didAnything = true;
                Console.WriteLine($"Writing skills config: enabled={enabled} path={settings.SkillsPath}");
                var result = await codex.WriteSkillsConfigAsync(enabled, settings.SkillsPath, ct);
                Console.WriteLine($"Result: effectiveEnabled={result.EffectiveEnabled?.ToString() ?? "n/a"}");
            }

            if (!didAnything)
            {
                Console.Error.WriteLine("Nothing to do. Provide --remote-skill-id/--remote-skill-name and/or --skills-enabled + --skills-path.");
                return 1;
            }

            return 0;
        });
}

public sealed class AppServerConfigWriteSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--apply")]
    public bool Apply { get; init; }

    [CommandOption("--remote-skill-id <ID>")]
    public string? RemoteSkillId { get; init; }

    [CommandOption("--remote-skill-name <NAME>")]
    public string? RemoteSkillName { get; init; }

    [CommandOption("--preload")]
    public bool Preload { get; init; }

    [CommandOption("--skills-enabled <BOOL>")]
    public bool? SkillsEnabled { get; init; }

    [CommandOption("--skills-path <PATH>")]
    public string? SkillsPath { get; init; }
}

