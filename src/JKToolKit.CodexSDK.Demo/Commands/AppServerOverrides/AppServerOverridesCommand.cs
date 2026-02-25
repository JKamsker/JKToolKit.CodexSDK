using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOverrides;

public sealed class AppServerOverridesCommand : AsyncCommand<AppServerOverridesSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerOverridesSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        try
        {
            Console.CancelKeyPress += cancelHandler;

            var responseTransformer = new MarkerResponseTransformer(maxLogLines: settings.PrintLimit);
            var notificationTransformer = new MarkerNotificationTransformer(maxLogLines: settings.PrintLimit);
            var notificationMapper = new MarkerNotificationMapper(maxLogLines: settings.PrintLimit);

            await using var sdk = CodexSdk.Create(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
                builder.ConfigureAppServer(o =>
                {
                    o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK AppServer Overrides Demo", "1.0.0");
                    o.ExperimentalApi = settings.ExperimentalApi;
                    o.ResponseTransformers = new IAppServerResponseTransformer[] { responseTransformer };
                    o.NotificationTransformers = new IAppServerNotificationTransformer[] { notificationTransformer };
                    o.NotificationMappers = new IAppServerNotificationMapper[] { notificationMapper };
                });
            });

            await using var codex = await sdk.AppServer.StartAsync(ct);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = CodexModel.Gpt52Codex,
                Cwd = repoPath,
                ApprovalPolicy = CodexApprovalPolicy.Never,
                Sandbox = CodexSandboxMode.WorkspaceWrite
            }, ct);

            Console.WriteLine($"Thread: {thread.Id}");

            // Trigger at least one request/response.
            _ = await codex.ListSkillsAsync(new SkillsListOptions { Cwd = repoPath }, ct);

            Console.WriteLine($"> {settings.Prompt}");
            Console.WriteLine();

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text(settings.Prompt)]
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

            Console.WriteLine();
            Console.WriteLine($"ResponseTransformer calls: {responseTransformer.CallCount}");
            Console.WriteLine($"NotificationTransformer calls: {notificationTransformer.CallCount}");
            Console.WriteLine($"NotificationMapper calls: {notificationMapper.CallCount}");

            if (responseTransformer.CallCount <= 0 ||
                notificationTransformer.CallCount <= 0 ||
                notificationMapper.CallCount <= 0)
            {
                Console.Error.WriteLine("Expected override hooks to be invoked (response + notification transformer + mapper).");
                return 1;
            }

            Console.WriteLine("ok");
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
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private sealed class MarkerResponseTransformer : IAppServerResponseTransformer
    {
        private readonly int _maxLogLines;
        private int _logged;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public MarkerResponseTransformer(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public JsonElement Transform(string method, JsonElement result)
        {
            Interlocked.Increment(ref _callCount);
            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[response-transformer] {method} resultKind={result.ValueKind}");
            }

            return result;
        }
    }

    private sealed class MarkerNotificationTransformer : IAppServerNotificationTransformer
    {
        private readonly int _maxLogLines;
        private int _logged;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public MarkerNotificationTransformer(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public (string Method, JsonElement Params) Transform(string method, JsonElement @params)
        {
            Interlocked.Increment(ref _callCount);
            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[notification-transformer] {method} paramsKind={@params.ValueKind}");
            }

            return (method, @params);
        }
    }

    private sealed class MarkerNotificationMapper : IAppServerNotificationMapper
    {
        private readonly int _maxLogLines;
        private int _logged;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public MarkerNotificationMapper(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public AppServerNotification? TryMap(string method, JsonElement @params)
        {
            Interlocked.Increment(ref _callCount);
            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[notification-mapper] {method} paramsKind={@params.ValueKind}");
            }

            return null;
        }
    }
}
