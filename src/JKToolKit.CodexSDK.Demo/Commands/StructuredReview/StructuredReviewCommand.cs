using System.Text;
using System.Diagnostics;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using Spectre.Console;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.StructuredReview;

public sealed class StructuredReviewCommand : AsyncCommand<StructuredReviewSettings>
{
    private const string BaseReviewPrompt =
        """
        Review the code changes described below. Look for bugs, security issues, performance problems,
        and style/maintainability concerns. For each issue found, note the file, line range, category,
        severity, and a clear description.

        Then produce an ordered list of fix tasks (highest priority first). Each task should have a
        title, description, priority (1-5), and the list of affected files.

        If the code is clean, return an empty issues and fixTasks array with a summary saying so.

        Return your response as JSON only, matching the required schema.
        """;

    private static string TimestampPrefix() =>
        $"[dim][[{DateTime.Now:dd.MM.yyyy HH:mm:ss}]] [/]";

    private static void LogLine(string markup) =>
        AnsiConsole.MarkupLine($"{TimestampPrefix()}{markup}");

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        StructuredReviewSettings settings,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        var ct = cts.Token;

        var workingDirectory = settings.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var customPrompt = ResolveCustomPrompt(settings);
        if (customPrompt is null)
        {
            var scopeCount = 0;
            if (!string.IsNullOrWhiteSpace(settings.BaseBranch)) scopeCount++;
            if (!string.IsNullOrWhiteSpace(settings.CommitSha)) scopeCount++;
            if (!string.IsNullOrWhiteSpace(settings.CommitsSince)) scopeCount++;

            if (scopeCount > 1)
            {
                LogLine("[red]Invalid scope options: choose only one of --base, --commit, or --since (or omit all to review the full repository).[/]");
                if (cancelHandler is not null)
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
                return 1;
            }
        }
        else if (!string.IsNullOrWhiteSpace(settings.BaseBranch)
                 || !string.IsNullOrWhiteSpace(settings.CommitSha)
                 || !string.IsNullOrWhiteSpace(settings.CommitsSince))
        {
            LogLine("[yellow]Note: scope options (--base/--commit/--since) are ignored when an explicit prompt is provided.[/]");
        }

        var prompt = BuildPrompt(settings, customPrompt);

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? CodexModel.Default
            : CodexModel.Parse(settings.Model);

        var reasoning = string.IsNullOrWhiteSpace(settings.Reasoning)
            ? CodexReasoningEffort.Medium
            : CodexReasoningEffort.Parse(settings.Reasoning);

        LogLine("[bold]Structured Review[/]");
        LogLine($"[dim]Directory:[/]    {Markup.Escape(workingDirectory)}");
        LogLine($"[dim]Model:[/]        {Markup.Escape(model.Value)}");
        LogLine($"[dim]Reasoning:[/]    {Markup.Escape(reasoning.Value)}");
        LogLine($"[dim]Attempts:[/]     {settings.MaxAttempts}");

        if (!string.IsNullOrWhiteSpace(settings.BaseBranch))
            LogLine($"[dim]Base branch:[/]  {Markup.Escape(settings.BaseBranch)}");
        if (!string.IsNullOrWhiteSpace(settings.CommitSha))
            LogLine($"[dim]Commit:[/]       {Markup.Escape(settings.CommitSha)}");
        if (!string.IsNullOrWhiteSpace(settings.CommitsSince))
            LogLine($"[dim]Since:[/]        {Markup.Escape(settings.CommitsSince)}");

        AnsiConsole.WriteLine();
        LogLine("[dim]Press Ctrl+C to cancel.[/]");

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
        });

        try
        {
            var consoleLock = new object();
            var started = Stopwatch.StartNew();
            var lastActivityUtc = DateTimeOffset.UtcNow;
            var turns = 0;
            var events = 0;

            var sessionOptions = new CodexSessionOptions(workingDirectory, prompt)
            {
                Model = model,
                ReasoningEffort = reasoning
            };

            LogLine("[yellow]Running structured review...[/]");

            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var progress = new CodexStructuredRunProgress
            {
                AttemptStarting = (attempt, max, kind) =>
                {
                    lock (consoleLock)
                    {
                        lastActivityUtc = DateTimeOffset.UtcNow;
                        LogLine($"[dim]Attempt {attempt}/{max} ({kind})[/]");
                    }
                },
                SessionLocated = (sid, logPath) =>
                {
                    lock (consoleLock)
                    {
                        lastActivityUtc = DateTimeOffset.UtcNow;
                        LogLine($"[dim]Session:[/] {Markup.Escape(sid.Value)}");
                        if (!string.IsNullOrWhiteSpace(logPath))
                        {
                            LogLine($"[dim]Log:[/]     {Markup.Escape(logPath)}");
                        }
                    }
                },
                EventReceived = evt =>
                {
                    lock (consoleLock)
                    {
                        lastActivityUtc = DateTimeOffset.UtcNow;
                        events++;

                        switch (evt)
                        {
                            case TurnContextEvent ctx:
                                turns++;
                                LogLine(
                                    $"[dim]Turn {turns}:[/] approval={Markup.Escape(ctx.ApprovalPolicy ?? "n/a")}, sandbox={Markup.Escape(ctx.SandboxPolicyType ?? "n/a")}");
                                break;

                            case TokenCountEvent tokens:
                                var input = tokens.InputTokens?.ToString() ?? "n/a";
                                var output = tokens.OutputTokens?.ToString() ?? "n/a";
                                var reasoningTokens = tokens.ReasoningTokens?.ToString() ?? "n/a";
                                LogLine($"[dim]Tokens:[/] in={input}, out={output}, reasoning={reasoningTokens}");
                                break;

                            case TaskCompleteEvent:
                                LogLine("[dim]Task complete event received.[/]");
                                break;
                        }
                    }
                },
                ParseFailed = (attempt, ex) =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    lock (consoleLock)
                    {
                        lastActivityUtc = DateTimeOffset.UtcNow;
                        LogLine($"[yellow]Parse failed on attempt {attempt}: {Markup.Escape(ex.Message)}[/]");
                    }
                }
            };

            var heartbeatTask = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
                try
                {
                    while (await timer.WaitForNextTickAsync(progressCts.Token).ConfigureAwait(false))
                    {
                        var now = DateTimeOffset.UtcNow;
                        int currentTurns;
                        int currentEvents;
                        var shouldLog = false;
                        lock (consoleLock)
                        {
                            currentTurns = turns;
                            currentEvents = events;
                            if (now - lastActivityUtc >= TimeSpan.FromSeconds(20))
                            {
                                lastActivityUtc = now;
                                shouldLog = true;
                            }
                        }

                        if (!shouldLog)
                        {
                            continue;
                        }

                        lock (consoleLock)
                        {
                            LogLine($"[dim]...running ({started.Elapsed:hh\\:mm\\:ss}) turns={currentTurns}, events={currentEvents}[/]");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected
                }
            }, CancellationToken.None);

            CodexStructuredResult<StructuredReviewResult> result;
            try
            {
                result = await sdk.Exec.RunStructuredWithRetryAsync<StructuredReviewResult>(
                    sessionOptions,
                    progress,
                    retry: new CodexStructuredRetryOptions { MaxAttempts = settings.MaxAttempts },
                    ct: ct);
            }
            finally
            {
                progressCts.Cancel();
                try { await heartbeatTask.ConfigureAwait(false); } catch { /* best-effort */ }
            }

            if (result.Value is null)
            {
                LogLine("[red]Structured review returned no result (null).[/]");
                return 1;
            }

            RenderResult(result.Value);

            AnsiConsole.WriteLine();
            LogLine($"[dim]Session:[/] {Markup.Escape(result.SessionId ?? string.Empty)}");

            return 0;
        }
        catch (CodexStructuredOutputRetryFailedException ex)
        {
            LogLine($"[red]Failed after {ex.Attempts} attempts.[/]");
            LogLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        catch (OperationCanceledException)
        {
            LogLine("[yellow]Cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
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

    private static void RenderResult(StructuredReviewResult review)
    {
        var summary = review.Summary ?? string.Empty;
        var severity = review.Severity ?? "unknown";
        var issues = review.Issues ?? [];
        var fixTasks = review.FixTasks ?? [];

        // Summary panel
        var severityColor = severity.ToLowerInvariant() switch
        {
            "clean" => "green",
            "low" => "blue",
            "medium" => "yellow",
            "high" => "red",
            "critical" => "red bold",
            _ => "white"
        };

        AnsiConsole.Write(new Panel(summary)
            .Header($"[{severityColor}]Review — {severity.ToUpperInvariant()}[/]")
            .BorderColor(severity.ToLowerInvariant() is "clean" or "low" ? Color.Green : Color.Yellow));

        // Issues table
        if (issues.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Issues ({issues.Count}):[/]");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("File")
                .AddColumn("Lines")
                .AddColumn("Category")
                .AddColumn("Severity")
                .AddColumn("Description");

            foreach (var issue in issues)
            {
                var issueFilePath = issue.FilePath ?? "(unknown)";
                var issueCategory = issue.Category ?? "other";
                var issueSeverity = issue.Severity ?? "info";
                var issueDescription = issue.Description ?? string.Empty;

                var issueSevColor = issueSeverity.ToLowerInvariant() switch
                {
                    "critical" => "red bold",
                    "error" => "red",
                    "warning" => "yellow",
                    _ => "dim"
                };

                table.AddRow(
                    Markup.Escape(issueFilePath),
                    Markup.Escape(issue.LineRange ?? "-"),
                    Markup.Escape(issueCategory),
                    $"[{issueSevColor}]{Markup.Escape(issueSeverity)}[/]",
                    Markup.Escape(issueDescription));
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No issues found.[/]");
        }

        // Fix tasks
        if (fixTasks.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Fix Tasks ({fixTasks.Count}):[/]");

            foreach (var task in fixTasks.OrderBy(t => t.Priority))
            {
                var title = task.Title ?? "(untitled)";
                var description = task.Description ?? string.Empty;

                var tree = new Tree($"[bold]P{task.Priority}[/] {Markup.Escape(title)}");
                tree.AddNode($"[dim]{Markup.Escape(description)}[/]");
                tree.AddNode($"Files: {Markup.Escape(string.Join(", ", task.AffectedFiles ?? []))}");
                AnsiConsole.Write(tree);
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No fix tasks needed.[/]");
        }
    }

    private static string BuildPrompt(StructuredReviewSettings settings, string? customPrompt)
    {
        // If the user supplied an explicit prompt, use it as-is.
        if (customPrompt is not null)
            return customPrompt;

        // Build a scope-aware prompt from the git params.
        var sb = new StringBuilder();
        sb.AppendLine(BaseReviewPrompt);

        var hasScope = false;

        if (!string.IsNullOrWhiteSpace(settings.BaseBranch))
        {
            sb.AppendLine();
            sb.AppendLine($"Scope: review the diff of the current branch compared to base branch `{settings.BaseBranch}`.");
            sb.AppendLine($"Use `git diff {settings.BaseBranch}...HEAD` to obtain the changes.");
            hasScope = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.CommitSha))
        {
            sb.AppendLine();
            sb.AppendLine($"Scope: review only commit `{settings.CommitSha}`.");
            sb.AppendLine($"Use `git show {settings.CommitSha}` to obtain the changes.");
            hasScope = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.CommitsSince))
        {
            sb.AppendLine();
            sb.AppendLine($"Scope: review all commits since `{settings.CommitsSince}` (exclusive).");
            sb.AppendLine($"Use `git log {settings.CommitsSince}..HEAD` and `git diff {settings.CommitsSince}..HEAD` to obtain the changes.");
            hasScope = true;
        }

        if (!hasScope)
        {
            sb.AppendLine();
            sb.AppendLine("Scope: review the entire repository.");
        }

        return sb.ToString();
    }

    private static string? ResolveCustomPrompt(StructuredReviewSettings settings)
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

        return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
    }
}
