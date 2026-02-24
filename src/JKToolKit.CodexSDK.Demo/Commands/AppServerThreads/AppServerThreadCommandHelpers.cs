using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

internal static class AppServerThreadCommandHelpers
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true
    };

    public static string ResolveRepoPath(AppServerThreadsSettingsBase settings) =>
        settings.RepoPath ?? Directory.GetCurrentDirectory();

    public static async Task<int> RunWithClientAsync(
        AppServerThreadsSettingsBase settings,
        CancellationToken cancellationToken,
        Func<CodexAppServerClient, CancellationToken, Task<int>> action)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        var ct = cts.Token;

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK AppServer Demo", "1.0.0"));
        });

        try
        {
            await using var codex = await sdk.AppServer.StartAsync(ct);
            return await action(codex, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
    }

    public static void PrintJson(JsonElement json) =>
        Console.WriteLine(JsonSerializer.Serialize(json, IndentedJson));
}

