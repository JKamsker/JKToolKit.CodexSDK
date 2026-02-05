using System.Diagnostics;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecCommand : AsyncCommand<ExecSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ExecSettings settings, CancellationToken cancellationToken)
    {
        var prompt = ResolvePrompt(settings);
        var workingDirectory = settings.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var sessionsRoot =
            settings.SessionsRoot ??
            (!string.IsNullOrWhiteSpace(settings.CodexHomeDirectory)
                ? Path.Combine(settings.CodexHomeDirectory, "sessions")
                : DefaultSessionsRoot());

        Directory.CreateDirectory(sessionsRoot);

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? CodexModel.Default
            : CodexModel.Parse(settings.Model);

        var reasoning = string.IsNullOrWhiteSpace(settings.Reasoning)
            ? CodexReasoningEffort.Medium
            : CodexReasoningEffort.Parse(settings.Reasoning);

        var followStream = !settings.NoFollow;

        PrintBanner();
        PrintConfig(workingDirectory, prompt, sessionsRoot, settings.CodexExecutablePath, model, reasoning, followStream);

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownCts.Cancel();
        };
        var ct = shutdownCts.Token;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug)
                .AddFilter("JKToolKit.CodexSDK.Exec.CodexClient", LogLevel.Debug)
                .AddFilter("JKToolKit.CodexSDK.*", LogLevel.Information);
        });

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.UseLoggerFactory(loggerFactory);
            builder.ConfigureExec(o => o.SessionsRootDirectory = sessionsRoot);
        });

        try
        {
            await ShowRateLimitsAsync(sdk.Exec, noCache: false, ct);
            await RefreshRateLimitsAsync(sdk.Exec, workingDirectory, model, reasoning, ct);
            await ShowRateLimitsAsync(sdk.Exec, noCache: true, ct);

            var sessionOptions = new CodexSessionOptions(workingDirectory, prompt)
            {
                Model = model,
                ReasoningEffort = reasoning
            };

            Console.WriteLine("Starting Codex session...");
            var startSw = Stopwatch.StartNew();
            await using var session = await sdk.Exec.StartSessionAsync(sessionOptions, ct);
            startSw.Stop();
            Console.WriteLine($"StartSessionAsync completed in {startSw.ElapsedMilliseconds} ms");

            PrintSessionInfo(session.Info);
            Console.WriteLine("\nStreaming events (press Ctrl+C to stop):\n");

            var streamOptions = EventStreamOptions.Default with { Follow = followStream };
            await foreach (var evt in session.GetEventsAsync(streamOptions, ct))
            {
                RenderEvent(evt);
            }

            if (!followStream && session.IsLive)
            {
                Console.WriteLine("\nSession is still live; waiting for graceful exit...");
                await session.WaitForExitAsync(ct);
            }

            Console.WriteLine("\nResuming the session with follow-up: \"how are you\" ...\n");
            var followUpOptions = sessionOptions.Clone();
            followUpOptions.Prompt = "how are you";
            await using var resumed = await sdk.Exec.ResumeSessionAsync(session.Info.Id, followUpOptions, ct);

            await foreach (var evt in resumed.GetEventsAsync(EventStreamOptions.Default with { Follow = true }, ct))
            {
                RenderEvent(evt);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nCancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("\nDemo failed:");
            Console.Error.WriteLine(ex.Message);
            await PrintDebugInfo(sessionsRoot);
            return 1;
        }
    }

    private static string ResolvePrompt(ExecSettings settings)
    {
        var prompt = settings.PromptOption;
        if (string.IsNullOrWhiteSpace(prompt) && settings.Prompt.Length > 0)
        {
            prompt = string.Join(" ", settings.Prompt);
        }

        if (string.Equals(prompt, "-", StringComparison.Ordinal))
        {
            return Console.In.ReadToEnd();
        }

        return string.IsNullOrWhiteSpace(prompt)
            ? "Summarize this repository in three concise bullet points."
            : prompt;
    }

    private static string DefaultSessionsRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");

    private static void PrintBanner()
    {
        Console.WriteLine("================================");
        Console.WriteLine("      JKToolKit.CodexSDK Demo    ");
        Console.WriteLine("================================");
    }

    private static void PrintConfig(
        string workingDirectory,
        string prompt,
        string sessionsRoot,
        string? codexExecutablePath,
        CodexModel model,
        CodexReasoningEffort reasoning,
        bool followStream)
    {
        Console.WriteLine($"Working directory : {workingDirectory}");
        Console.WriteLine($"Prompt            : {prompt}");
        Console.WriteLine($"Sessions root     : {sessionsRoot}");
        Console.WriteLine($"Model             : {model.Value}");
        Console.WriteLine($"Reasoning effort  : {reasoning.Value}");
        Console.WriteLine($"Follow stream     : {followStream}");
        Console.WriteLine($"Codex path        : {(string.IsNullOrWhiteSpace(codexExecutablePath) ? "default" : codexExecutablePath)}");
        Console.WriteLine("Ctrl+C to cancel at any time.\n");
    }

    private static void PrintSessionInfo(CodexSessionInfo info)
    {
        Console.WriteLine("Session started:");
        Console.WriteLine($"  Session ID : {info.Id.Value}");
        Console.WriteLine($"  Log file   : {info.LogPath}");
        Console.WriteLine($"  Created at : {info.CreatedAt:O}");
    }

    private static async Task ShowRateLimitsAsync(CodexExecFacade exec, bool noCache, CancellationToken ct)
    {
        var limits = await exec.GetRateLimitsAsync(noCache, ct);
        if (limits is null)
        {
            Console.WriteLine(noCache
                ? "Rate limits: none found after refresh."
                : "Rate limits: none cached/found yet.");
            return;
        }

        static string Scope(RateLimitScope? s) =>
            s is null
                ? "n/a"
                : $"used={s.UsedPercent?.ToString("0.#") ?? "?"}% window={s.WindowMinutes?.ToString() ?? "?"}m resets={s.ResetsAt?.ToLocalTime():G}";

        var primary = Scope(limits.Primary);
        var secondary = Scope(limits.Secondary);
        var credits = limits.Credits is null
            ? "n/a"
            : $"has={limits.Credits.HasCredits?.ToString() ?? "?"}, unlimited={limits.Credits.Unlimited?.ToString() ?? "?"}, balance={limits.Credits.Balance ?? "?"}";

        Console.WriteLine($"Rate limits (noCache={noCache}): primary [{primary}], secondary [{secondary}], credits [{credits}]");
    }

    private static async Task RefreshRateLimitsAsync(
        CodexExecFacade exec,
        string workingDirectory,
        CodexModel model,
        CodexReasoningEffort reasoning,
        CancellationToken ct)
    {
        var refreshOpts = new CodexSessionOptions(workingDirectory, "hi")
        {
            Model = model,
            ReasoningEffort = reasoning,
            AdditionalOptions = Array.Empty<string>()
        };

        await using var session = await exec.StartSessionAsync(refreshOpts, ct);
        await foreach (var _ in session.GetEventsAsync(EventStreamOptions.Default with { Follow = false }, ct))
        {
        }
    }

    private static void RenderEvent(CodexEvent evt)
    {
        var timestamp = evt.Timestamp.ToLocalTime().ToString("HH:mm:ss");

        switch (evt)
        {
            case UserMessageEvent user:
                Console.WriteLine($"[{timestamp}] user   > {user.Text}");
                break;
            case AgentMessageEvent agent:
                Console.WriteLine($"[{timestamp}] codex  > {agent.Text}");
                break;
            case AgentReasoningEvent reasoning:
                Console.WriteLine($"[{timestamp}] chain  > {reasoning.Text}");
                break;
            case TurnContextEvent context:
                Console.WriteLine($"[{timestamp}] ctx    > approval={context.ApprovalPolicy ?? "n/a"}, sandbox={context.SandboxPolicyType ?? "n/a"}");
                break;
            case ResponseItemEvent responseItem:
                RenderResponseItem(timestamp, responseItem);
                break;
            case TokenCountEvent tokens:
                var input = tokens.InputTokens?.ToString() ?? "n/a";
                var output = tokens.OutputTokens?.ToString() ?? "n/a";
                var reasoningTokens = tokens.ReasoningTokens?.ToString() ?? "n/a";
                Console.WriteLine($"[{timestamp}] tokens > in={input}, out={output}, reasoning={reasoningTokens}");
                break;
            default:
                Console.WriteLine($"[{timestamp}] {evt.Type,-6}");
                break;
        }
    }

    private static void RenderResponseItem(string timestamp, ResponseItemEvent evt)
    {
        switch (evt.Payload)
        {
            case ReasoningResponseItemPayload reasoning when reasoning.SummaryTexts.Count > 0:
                Console.WriteLine($"[{timestamp}] resp   > {string.Join(" | ", reasoning.SummaryTexts)}");
                return;

            case MessageResponseItemPayload msg when msg.TextParts.Count > 0:
                Console.WriteLine($"[{timestamp}] resp-msg ({msg.Role ?? "n/a"}) > {string.Join(" ", msg.TextParts)}");
                return;

            case FunctionCallResponseItemPayload fn:
                Console.WriteLine($"[{timestamp}] resp-fn  > {fn.Name ?? "?"}({fn.ArgumentsJson ?? ""}) callId={fn.CallId ?? "n/a"}");
                return;

            case FunctionCallOutputResponseItemPayload fnOut:
                Console.WriteLine($"[{timestamp}] resp-fn-out > callId={fnOut.CallId ?? "n/a"}");
                return;

            case CustomToolCallResponseItemPayload tool:
                Console.WriteLine($"[{timestamp}] resp-tool > {tool.Name ?? "?"} callId={tool.CallId ?? "n/a"} status={tool.Status ?? "n/a"}");
                return;

            case CustomToolCallOutputResponseItemPayload toolOut:
                Console.WriteLine($"[{timestamp}] resp-tool-out > callId={toolOut.CallId ?? "n/a"}");
                return;

            case WebSearchCallResponseItemPayload web:
                Console.WriteLine($"[{timestamp}] resp-web > {web.Action?.Type ?? "search"} q={web.Action?.Query ?? "n/a"}");
                return;

            case GhostSnapshotResponseItemPayload ghost when ghost.GhostCommit?.Id is { } id:
                Console.WriteLine($"[{timestamp}] resp-ghost > {id}");
                return;

            case UnknownResponseItemPayload unk:
                Console.WriteLine($"[{timestamp}] resp[{unk.PayloadType}] > {unk.Raw.GetRawText()}");
                return;

            default:
                Console.WriteLine($"[{timestamp}] resp[{evt.PayloadType}]");
                return;
        }
    }

    private static async Task PrintDebugInfo(string sessionsRoot)
    {
        Console.Error.WriteLine("\nDebug info:");
        Console.Error.WriteLine($"  Sessions root: {sessionsRoot}");
        if (Directory.Exists(sessionsRoot))
        {
            var files = Directory.GetFiles(sessionsRoot, "*.jsonl");
            Console.Error.WriteLine(files.Length == 0
                ? "  (no session files present)"
                : $"  Found {files.Length} session file(s). Latest:");

            foreach (var file in files.OrderByDescending(File.GetCreationTimeUtc).Take(3))
            {
                Console.Error.WriteLine($"    - {file}");
            }
        }
        else
        {
            Console.Error.WriteLine("  Sessions root directory does not exist.");
        }

        await Task.CompletedTask;
    }
}
