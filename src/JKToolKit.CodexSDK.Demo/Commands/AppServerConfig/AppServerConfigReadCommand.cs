using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerConfig;

public sealed class AppServerConfigReadCommand : AsyncCommand<AppServerConfigReadSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerConfigReadSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            var cwd = string.IsNullOrWhiteSpace(settings.Cwd)
                ? AppServerThreadCommandHelpers.ResolveRepoPath(settings)
                : settings.Cwd;

            var config = await codex.ReadConfigAsync(new ConfigReadOptions
            {
                Cwd = cwd,
                IncludeLayers = settings.IncludeLayers
            }, ct);

            var requirements = await codex.ReadConfigRequirementsAsync(ct);
            RemoteSkillsReadResult? remoteSkills = null;
            Exception? remoteSkillsError = null;
            try
            {
                remoteSkills = await codex.ReadRemoteSkillsAsync(ct);
            }
            catch (Exception ex)
            {
                remoteSkillsError = ex;
            }
            var limit = settings.Limit <= 0 ? 25 : settings.Limit;

            Console.WriteLine($"config/read cwd: {cwd}");
            Console.WriteLine($"- top-level keys: {CountTopLevelKeys(config.Config)}");
            Console.WriteLine($"- layers: {config.Layers?.Count ?? 0} (includeLayers={settings.IncludeLayers})");
            Console.WriteLine($"- mcp servers: {config.McpServers?.Count ?? 0}");
            Console.WriteLine();

            if (config.Layers is { Count: > 0 })
            {
                Console.WriteLine("Layers:");
                foreach (var layer in config.Layers)
                {
                    var file = layer.Name.File ?? layer.Name.DotCodexFolder ?? string.Empty;
                    var disabled = string.IsNullOrWhiteSpace(layer.DisabledReason) ? "" : $" disabled='{layer.DisabledReason}'";
                    Console.WriteLine($"- {layer.Name.Type} v{layer.Version}{disabled}{(string.IsNullOrWhiteSpace(file) ? "" : $" ({file})")}");
                }

                Console.WriteLine();
            }

            if (config.McpServers is { Count: > 0 })
            {
                Console.WriteLine("MCP servers (from effective config):");
                foreach (var (name, server) in config.McpServers)
                {
                    var enabled = server.Enabled is null ? "" : $" enabled={server.Enabled}";
                    var required = server.Required is null ? "" : $" required={server.Required}";
                    Console.WriteLine($"- {name}: transport={server.Transport}{enabled}{required}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("configRequirements/read:");
            if (requirements.Requirements is null)
            {
                Console.WriteLine("- none configured");
            }
            else
            {
                PrintRequirements(requirements.Requirements);
            }

            Console.WriteLine();
            if (remoteSkills is not null)
            {
                Console.WriteLine($"remoteSkills/read: {remoteSkills.Skills.Count}");
                foreach (var s in remoteSkills.Skills.Take(limit))
                {
                    Console.WriteLine($"- {s.Name} ({s.Id})");
                }

                if (remoteSkills.Skills.Count > limit)
                {
                    Console.WriteLine($"... ({remoteSkills.Skills.Count - limit} more)");
                }
            }
            else
            {
                Console.WriteLine($"remoteSkills/read: (failed) {remoteSkillsError?.Message ?? "unknown error"}");
            }

            if (settings.Json)
            {
                Console.WriteLine();
                Console.WriteLine("[raw] configRequirements/read:");
                AppServerThreadCommandHelpers.PrintJson(requirements.Raw);

                if (remoteSkills is not null)
                {
                    Console.WriteLine("\n[raw] remoteSkills/read:");
                    AppServerThreadCommandHelpers.PrintJson(remoteSkills.Raw);
                }
            }

            return 0;
        });

    private static int CountTopLevelKeys(JsonElement config) =>
        config.ValueKind == JsonValueKind.Object ? config.EnumerateObject().Count() : 0;

    private static void PrintRequirements(ConfigRequirements r)
    {
        static string List<T>(IReadOnlyList<T>? values) =>
            values is null || values.Count == 0
                ? "n/a"
                : string.Join(", ", values.Select(v => v?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)));

        Console.WriteLine($"- allowedApprovalPolicies: {List(r.AllowedApprovalPolicies)}");
        Console.WriteLine($"- allowedSandboxModes:     {List(r.AllowedSandboxModes)}");
        Console.WriteLine($"- allowedWebSearchModes:   {List(r.AllowedWebSearchModes)}");
        Console.WriteLine($"- enforceResidency:        {r.EnforceResidency?.ToString() ?? "n/a"}");
        Console.WriteLine($"- network:                 {(r.Network is null ? "n/a" : "present")}");
    }
}

public sealed class AppServerConfigReadSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--cwd <DIR>")]
    public string? Cwd { get; init; }

    [CommandOption("--include-layers")]
    public bool IncludeLayers { get; init; }

    [CommandOption("--limit <N>")]
    public int Limit { get; init; } = 25;
}
