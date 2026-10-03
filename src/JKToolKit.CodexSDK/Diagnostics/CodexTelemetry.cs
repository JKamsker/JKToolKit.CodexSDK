using System.Diagnostics;
using System.Diagnostics.Metrics;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Diagnostics;

/// <summary>OpenTelemetry-compatible diagnostics. Prompt, response, paths and account details are never recorded.</summary>
public static class CodexTelemetry
{
    /// <summary>The instrumentation name for registering an ActivitySource and Meter with OpenTelemetry.</summary>
    public const string Name = "JKToolKit.CodexSDK";
    /// <summary>Activities for high-level turns, parented to the caller's current activity.</summary>
    public static ActivitySource ActivitySource { get; } = new(Name);
    /// <summary>Turn duration and outcome metrics.</summary>
    public static Meter Meter { get; } = new(Name);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("codex.turn.duration", "s");
    private static readonly Counter<long> Turns = Meter.CreateCounter<long>("codex.turn.count");

    internal static async Task<CodexTurnHandle> StartTurnAsync(CodexTurnContext context,
        Func<CancellationToken, Task<CodexTurnHandle>> start, CancellationToken ct)
    {
        var activity = ActivitySource.StartActivity("codex.turn", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "invoke_agent");
        activity?.SetTag("gen_ai.provider.name", "openai");
        activity?.SetTag("gen_ai.conversation.id", context.ThreadId);
        activity?.SetTag("gen_ai.request.model", context.Options.Model?.Value);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var handle = await start(ct).ConfigureAwait(false);
            activity?.SetTag("codex.turn.id", handle.TurnId);
            _ = FinishAsync(handle, activity, started);
            return handle;
        }
        catch (Exception ex)
        {
            Record(activity, started, ex is OperationCanceledException ? "canceled" : "error", ex.GetType().Name);
            throw;
        }
    }

    private static async Task FinishAsync(CodexTurnHandle handle, Activity? activity, long started)
    {
        try
        {
            var result = await handle.RunAsync().ConfigureAwait(false);
            Record(activity, started, result.Status ?? "unknown", result.Error is null ? null : "turn_failed");
        }
        catch (Exception ex) { Record(activity, started, ex is OperationCanceledException ? "canceled" : "error", ex.GetType().Name); }
    }

    private static void Record(Activity? activity, long started, string status, string? error)
    {
        activity?.SetTag("codex.turn.status", status);
        if (error is not null) { activity?.SetTag("error.type", error); activity?.SetStatus(ActivityStatusCode.Error); }
        activity?.Dispose();
        // Unknown protocol statuses are collapsed to keep metric cardinality bounded.
        var outcome = status is "completed" or "failed" or "interrupted" or "canceled" or "error" ? status : "unknown";
        var tags = new TagList { { "codex.turn.status", outcome } };
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        Turns.Add(1, tags);
    }
}
