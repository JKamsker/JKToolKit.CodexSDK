using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerFuzzyFileSearch;

public sealed class AppServerFuzzyFileSearchCommand : AsyncCommand<AppServerFuzzyFileSearchSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AppServerFuzzyFileSearchSettings settings, CancellationToken cancellationToken) =>
        AppServerThreadCommandHelpers.RunWithClientAsync(settings, cancellationToken, async (codex, ct) =>
        {
            if (!settings.ExperimentalApi)
            {
                Console.Error.WriteLine("This demo requires --experimental-api.");
                return 1;
            }

            var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);
            var roots = new List<string> { repoPath };
            if (!string.IsNullOrWhiteSpace(settings.Root) && !roots.Contains(settings.Root, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(settings.Root);
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var query = string.IsNullOrWhiteSpace(settings.Query) ? "Codex" : settings.Query;

            Console.WriteLine($"Starting fuzzy file search session: {sessionId}");
            Console.WriteLine($"Roots: {string.Join(", ", roots)}");
            Console.WriteLine($"Query: {query}");
            Console.WriteLine();

            using var notificationsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var updates = 0;

            var notificationsTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var n in codex.Notifications(notificationsCts.Token))
                    {
                        if (n is FuzzyFileSearchSessionUpdatedNotification u && u.SessionId == sessionId)
                        {
                            updates++;
                            Console.WriteLine($"[update #{updates}] files={u.Files.Count}");
                            foreach (var f in u.Files.Take(10))
                            {
                                Console.WriteLine($"- {f.FileName} (score={f.Score}) {f.Path}");
                            }
                            Console.WriteLine();

                            if (updates >= settings.MaxUpdates)
                            {
                                completed.TrySetResult(true);
                                return;
                            }
                        }
                        else if (n is FuzzyFileSearchSessionCompletedNotification c && c.SessionId == sessionId)
                        {
                            completed.TrySetResult(true);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (notificationsCts.IsCancellationRequested)
                {
                    // ignore
                }
            }, CancellationToken.None);

            await codex.StartFuzzyFileSearchSessionAsync(sessionId, roots, ct);
            await codex.UpdateFuzzyFileSearchSessionAsync(sessionId, query, ct);

            var waitSeconds = settings.WaitSeconds <= 0 ? 5 : settings.WaitSeconds;
            await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct));

            await codex.StopFuzzyFileSearchSessionAsync(sessionId, ct);

            // Best-effort: wait briefly for a completed notification.
            await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(2), ct));

            notificationsCts.Cancel();
            try { await notificationsTask.ConfigureAwait(false); } catch { /* ignore */ }

            Console.WriteLine("Done.");
            return 0;
        });
}

public sealed class AppServerFuzzyFileSearchSettings : AppServerThreadsSettingsBase
{
    [CommandOption("--root <PATH>")]
    public string? Root { get; init; }

    [CommandOption("--query <TEXT>")]
    public string? Query { get; init; }

    [CommandOption("--wait-seconds <N>")]
    public int WaitSeconds { get; init; } = 5;

    [CommandOption("--max-updates <N>")]
    public int MaxUpdates { get; init; } = 2;
}

