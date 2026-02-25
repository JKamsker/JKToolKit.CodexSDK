using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Overrides;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.ExecOverrides;

public sealed class ExecOverridesCommand : AsyncCommand<ExecOverridesSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ExecOverridesSettings settings, CancellationToken cancellationToken)
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
            ? CodexModel.Default
            : CodexModel.Parse(settings.Model);

        var reasoning = string.IsNullOrWhiteSpace(settings.Reasoning)
            ? CodexReasoningEffort.Low
            : CodexReasoningEffort.Parse(settings.Reasoning);

        var prompt = string.IsNullOrWhiteSpace(settings.Prompt)
            ? "Say 'ok' and nothing else."
            : settings.Prompt;

        using var loggerFactory = LoggerFactory.Create(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning));

        var transformer = new FirstEventTypeTransformer(maxLogLines: 5);
        var mapper = new DemoEventMapper(maxLogLines: 5);

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.UseLoggerFactory(loggerFactory);
            builder.ConfigureExec(o =>
            {
                o.EventTransformers = new IExecEventTransformer[] { transformer };
                o.EventMappers = new IExecEventMapper[] { mapper };
            });
        });

        try
        {
            var sessionOptions = new CodexSessionOptions(repoPath, prompt)
            {
                Model = model,
                ReasoningEffort = reasoning
            };

            Console.WriteLine($"Repo: {repoPath}");
            Console.WriteLine($"Model: {model.Value}");
            Console.WriteLine($"Reasoning: {reasoning.Value}");
            Console.WriteLine($"Prompt: {prompt}");
            Console.WriteLine();

            await using var session = await sdk.Exec.StartSessionAsync(sessionOptions, ct);

            var customEventSeen = false;
            await foreach (var ev in session.GetEventsAsync(EventStreamOptions.Default with { Follow = true }, ct))
            {
                if (ev is DemoCustomEvent demo)
                {
                    customEventSeen = true;
                    Console.WriteLine($"[custom-event] {demo.Marker} rawKind={demo.RawPayload.ValueKind}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Transformer calls: {transformer.CallCount}");
            Console.WriteLine($"Mapper calls: {mapper.CallCount}");
            Console.WriteLine($"Custom event seen: {customEventSeen}");

            if (transformer.CallCount <= 0 || mapper.CallCount <= 0 || !customEventSeen)
            {
                Console.Error.WriteLine("Expected override hooks to be invoked (transformer + mapper + custom event).");
                return 1;
            }

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
    }

    private sealed class FirstEventTypeTransformer : IExecEventTransformer
    {
        private readonly int _maxLogLines;
        private int _logged;
        private int _transformed;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public FirstEventTypeTransformer(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload)
        {
            Interlocked.Increment(ref _callCount);

            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[transformer] {type} ({rawPayload.ValueKind})");
            }

            // Don't rewrite `session_meta` because the exec client uses it for the initial session handshake.
            if (!string.Equals(type, "session_meta", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref _transformed, 1, 0) == 0)
            {
                Console.WriteLine($"[transformer] rewriting first event type to 'demo/custom' (was '{type}')");
                return ("demo/custom", rawPayload);
            }

            return (type, rawPayload);
        }
    }

    private sealed class DemoEventMapper : IExecEventMapper
    {
        private readonly int _maxLogLines;
        private int _logged;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public DemoEventMapper(int maxLogLines)
        {
            _maxLogLines = maxLogLines;
        }

        public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload)
        {
            Interlocked.Increment(ref _callCount);

            if (Interlocked.Increment(ref _logged) <= _maxLogLines)
            {
                Console.WriteLine($"[mapper] {type} ({rawPayload.ValueKind})");
            }

            if (!string.Equals(type, "demo/custom", StringComparison.Ordinal))
            {
                return null;
            }

            return new DemoCustomEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = rawPayload,
                Marker = "mapped-by-demo"
            };
        }
    }

    private sealed record DemoCustomEvent : CodexEvent
    {
        public required string Marker { get; init; }
    }
}
