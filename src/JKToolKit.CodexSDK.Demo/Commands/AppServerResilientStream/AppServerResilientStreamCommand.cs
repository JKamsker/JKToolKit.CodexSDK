using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerResilientStream;

public sealed class AppServerResilientStreamCommand : AsyncCommand<AppServerResilientStreamSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerResilientStreamSettings settings, CancellationToken cancellationToken)
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

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? CodexModel.Gpt52Codex
            : CodexModel.Parse(settings.Model);

        var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
            ? CodexApprovalPolicy.Never
            : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);

        var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
            ? CodexSandboxMode.WorkspaceWrite
            : CodexSandboxMode.Parse(settings.Sandbox);

        var services = new ServiceCollection();
        services.AddLogging(b => b
            .SetMinimumLevel(LogLevel.Warning)
            .AddConsole());

        services.AddCodexAppServerClient(o =>
        {
            o.CodexExecutablePath = settings.CodexExecutablePath;
            o.CodexHomeDirectory = settings.CodexHomeDirectory;
            o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK Resilient AppServer Demo", "1.0.0");
        });

        services.AddCodexResilientAppServerClient(o =>
        {
            // Safe default: auto-restart on, no automatic retries (user decides via RetryPolicy).
            o.AutoRestart = true;
            o.NotificationsContinueAcrossRestarts = true;
            o.EmitRestartMarkerNotifications = true;
        });

        var sp = services.BuildServiceProvider();

        try
        {
            var factory = sp.GetRequiredService<ICodexResilientAppServerClientFactory>();
            await using var codex = await factory.StartAsync(ct);

            using var notificationsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var notificationsTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var n in codex.Notifications(notificationsCts.Token))
                    {
                        if (n is ClientRestartedNotification r)
                        {
                            Console.Error.WriteLine(
                                $"[resiliency] restarted #{r.RestartCount} (exit={r.PreviousExitCode?.ToString() ?? "?"})");
                        }
                    }
                }
                catch (OperationCanceledException) when (notificationsCts.IsCancellationRequested)
                {
                    // ignore
                }
            }, notificationsCts.Token);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox
            }, ct);

            await RunTurnAsync(codex, thread.Id, settings.Prompt, ct);

            if (settings.RestartBetweenTurns)
            {
                Console.Error.WriteLine("[resiliency] forcing a manual restart...");
                await codex.RestartAsync(ct);

                // Best-effort: threads may be resumable across restarts depending on Codex persistence.
                try
                {
                    await codex.ResumeThreadAsync(thread.Id, ct);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[resiliency] thread resume failed, continuing with same id anyway: {ex.Message}");
                }
            }

            await RunTurnAsync(codex, thread.Id, settings.Prompt2, ct);

            notificationsCts.Cancel();
            try { await notificationsTask.ConfigureAwait(false); } catch { /* ignore */ }

            return 0;
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
            if (sp is IAsyncDisposable ad)
                await ad.DisposeAsync();
            else
                (sp as IDisposable)?.Dispose();
        }
    }

    private static async Task RunTurnAsync(
        ResilientCodexAppServerClient codex,
        string threadId,
        string prompt,
        CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine($"> {prompt}");
        Console.WriteLine();

        await using var turn = await codex.StartTurnAsync(threadId, new TurnStartOptions
        {
            Input = [TurnInputItem.Text(prompt)]
        }, ct);

        await foreach (var ev in turn.Events(ct))
        {
            if (ev is AgentMessageDeltaNotification delta)
            {
                Console.Write(delta.Delta);
            }
        }

        var completed = await turn.Completion;
        Console.WriteLine($"\nDone: {completed.Status}");
    }
}

