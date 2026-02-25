using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Overrides;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.McpServer.Overrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.DiOverrides;

public sealed class DiOverridesCommand : AsyncCommand<DiOverridesSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiOverridesSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = settings.RepoPath ?? Directory.GetCurrentDirectory();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var services = new ServiceCollection();
        services.AddLogging(b => b
            .SetMinimumLevel(LogLevel.Warning)
            .AddConsole());

        services.AddCodexSdk(
            exec =>
            {
                exec.CodexExecutablePath = settings.CodexExecutablePath;
                exec.CodexHomeDirectory = settings.CodexHomeDirectory;
            },
            appServer =>
            {
                appServer.CodexExecutablePath = settings.CodexExecutablePath;
                appServer.CodexHomeDirectory = settings.CodexHomeDirectory;
                appServer.MessageObservers = new IAppServerMessageObserver[] { new ConsoleObserver() };
                appServer.RequestParamsTransformers = new IAppServerRequestParamsTransformer[] { new NoopRequestParamsTransformer() };
            },
            mcpServer =>
            {
                mcpServer.CodexExecutablePath = settings.CodexExecutablePath;
                mcpServer.CodexHomeDirectory = settings.CodexHomeDirectory;
                mcpServer.ResponseTransformers = new IMcpServerResponseTransformer[] { new MarkerResponseTransformer() };
                mcpServer.ToolsListMappers = new IMcpToolsListMapper[] { new MarkerToolsListMapper() };
            });

        var sp = services.BuildServiceProvider();
        try
        {
            var sdk = sp.GetRequiredService<CodexSdk>();

            Console.WriteLine("[di] starting app-server...");
            await using (var app = await sdk.AppServer.StartAsync(ct))
            {
                _ = await app.ListSkillsAsync(new SkillsListOptions { Cwd = repoPath }, ct);
            }
            Console.WriteLine("[di] app-server ok");

            Console.WriteLine("[di] starting mcp-server...");
            await using (var mcp = await sdk.McpServer.StartAsync(ct))
            {
                _ = await mcp.ListToolsAsync(ct);
            }
            Console.WriteLine("[di] mcp-server ok");

            Console.WriteLine("ok");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (sp is IAsyncDisposable ad)
                await ad.DisposeAsync();
            else
                (sp as IDisposable)?.Dispose();
        }
    }

    private sealed class ConsoleObserver : IAppServerMessageObserver
    {
        public void OnRequest(string method, JsonElement @params) =>
            Console.WriteLine($"[observer] request {method} paramsKind={@params.ValueKind}");

        public void OnResponse(string method, JsonElement result) =>
            Console.WriteLine($"[observer] response {method} resultKind={result.ValueKind}");

        public void OnNotification(string method, JsonElement @params) =>
            Console.WriteLine($"[observer] notification {method} paramsKind={@params.ValueKind}");
    }

    private sealed class NoopRequestParamsTransformer : IAppServerRequestParamsTransformer
    {
        public JsonElement Transform(string method, JsonElement @params)
        {
            Console.WriteLine($"[request-transformer] {method} paramsKind={@params.ValueKind}");
            return @params;
        }
    }

    private sealed class MarkerResponseTransformer : IMcpServerResponseTransformer
    {
        public JsonElement Transform(string method, JsonElement result)
        {
            Console.WriteLine($"[mcp-response-transformer] {method}");
            return result;
        }
    }

    private sealed class MarkerToolsListMapper : IMcpToolsListMapper
    {
        public IReadOnlyList<McpToolDescriptor>? TryMap(JsonElement raw)
        {
            Console.WriteLine("[tools-list-mapper] invoked");
            return null;
        }
    }
}
