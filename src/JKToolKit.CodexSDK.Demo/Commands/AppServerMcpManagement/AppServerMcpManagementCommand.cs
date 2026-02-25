using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerMcpManagement;

public sealed class AppServerMcpManagementCommand : AsyncCommand<AppServerMcpManagementSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerMcpManagementSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (!settings.NoReload)
            {
                Console.WriteLine("Reloading MCP servers...");
                await codex.ReloadMcpServersAsync(ct);
            }

            var page = await codex.ListMcpServerStatusAsync(new McpServerStatusListOptions
            {
                Cursor = string.IsNullOrWhiteSpace(settings.Cursor) ? null : settings.Cursor,
                Limit = settings.Limit > 0 ? settings.Limit : null
            }, ct);

            if (settings.Json)
            {
                AppServerThreadCommandHelpers.PrintJson(page.Raw);
            }
            else
            {
                Console.WriteLine($"MCP servers: {page.Servers.Count}");
                foreach (var s in page.Servers)
                {
                    Console.WriteLine($"- {s.Name} auth={s.AuthStatus} tools={s.Tools.Count} resources={s.Resources.Count} templates={s.ResourceTemplates.Count}");
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.OauthName))
            {
                Console.WriteLine();
                Console.WriteLine($"Starting OAuth login for: {settings.OauthName}");

                var login = await codex.StartMcpServerOauthLoginAsync(new McpServerOauthLoginOptions
                {
                    Name = settings.OauthName,
                    TimeoutSeconds = settings.OauthTimeoutSeconds
                }, ct);

                Console.WriteLine($"AuthorizationUrl: {login.AuthorizationUrl}");
            }

            if (!string.IsNullOrWhiteSpace(page.NextCursor))
            {
                Console.WriteLine($"\nNextCursor: {page.NextCursor}");
            }

            return 0;
        });
}

public sealed class AppServerMcpManagementSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--cursor <CURSOR>")]
    public string? Cursor { get; init; }

    [CommandOption("--limit <N>")]
    public int Limit { get; init; } = 50;

    [CommandOption("--no-reload")]
    public bool NoReload { get; init; }

    [CommandOption("--oauth-name <NAME>")]
    public string? OauthName { get; init; }

    [CommandOption("--oauth-timeout-seconds <N>")]
    public long? OauthTimeoutSeconds { get; init; }
}

