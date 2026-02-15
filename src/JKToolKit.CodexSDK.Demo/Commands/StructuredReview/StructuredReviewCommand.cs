using System.Text;
using JKToolKit.CodexSDK.Exec;
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

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        StructuredReviewSettings settings,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var workingDirectory = settings.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var prompt = BuildPrompt(settings);

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? CodexModel.Default
            : CodexModel.Parse(settings.Model);

        var reasoning = string.IsNullOrWhiteSpace(settings.Reasoning)
            ? CodexReasoningEffort.Medium
            : CodexReasoningEffort.Parse(settings.Reasoning);

        AnsiConsole.MarkupLine("[bold]Structured Review[/]");
        AnsiConsole.MarkupLine($"[dim]Directory:[/]    {workingDirectory}");
        AnsiConsole.MarkupLine($"[dim]Model:[/]        {model.Value}");
        AnsiConsole.MarkupLine($"[dim]Reasoning:[/]    {reasoning.Value}");
        AnsiConsole.MarkupLine($"[dim]Retries:[/]      {settings.MaxRetries}");

        if (!string.IsNullOrWhiteSpace(settings.BaseBranch))
            AnsiConsole.MarkupLine($"[dim]Base branch:[/]  {settings.BaseBranch}");
        if (!string.IsNullOrWhiteSpace(settings.CommitSha))
            AnsiConsole.MarkupLine($"[dim]Commit:[/]       {settings.CommitSha}");
        if (!string.IsNullOrWhiteSpace(settings.CommitsSince))
            AnsiConsole.MarkupLine($"[dim]Since:[/]        {settings.CommitsSince}");

        AnsiConsole.WriteLine();

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
        });

        try
        {
            var sessionOptions = new CodexSessionOptions(workingDirectory, prompt)
            {
                Model = model,
                ReasoningEffort = reasoning
            };

            AnsiConsole.MarkupLine("[yellow]Running structured review...[/]");

            var result = await sdk.Exec.RunStructuredWithRetryAsync<StructuredReviewResult>(
                sessionOptions,
                retry: new CodexStructuredRetryOptions { MaxAttempts = settings.MaxRetries },
                ct: ct);

            RenderResult(result.Value);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Session:[/] {result.SessionId}");

            return 0;
        }
        catch (CodexStructuredOutputRetryFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed after {ex.Attempts} attempts.[/]");
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void RenderResult(StructuredReviewResult review)
    {
        // Summary panel
        var severityColor = review.Severity.ToLowerInvariant() switch
        {
            "clean" => "green",
            "low" => "blue",
            "medium" => "yellow",
            "high" => "red",
            "critical" => "red bold",
            _ => "white"
        };

        AnsiConsole.Write(new Panel(review.Summary)
            .Header($"[{severityColor}]Review — {review.Severity.ToUpperInvariant()}[/]")
            .BorderColor(review.Severity.ToLowerInvariant() is "clean" or "low" ? Color.Green : Color.Yellow));

        // Issues table
        if (review.Issues.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Issues ({review.Issues.Count}):[/]");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("File")
                .AddColumn("Lines")
                .AddColumn("Category")
                .AddColumn("Severity")
                .AddColumn("Description");

            foreach (var issue in review.Issues)
            {
                var issueSevColor = issue.Severity.ToLowerInvariant() switch
                {
                    "critical" => "red bold",
                    "error" => "red",
                    "warning" => "yellow",
                    _ => "dim"
                };

                table.AddRow(
                    Markup.Escape(issue.FilePath),
                    Markup.Escape(issue.LineRange ?? "-"),
                    Markup.Escape(issue.Category),
                    $"[{issueSevColor}]{Markup.Escape(issue.Severity)}[/]",
                    Markup.Escape(issue.Description));
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No issues found.[/]");
        }

        // Fix tasks
        if (review.FixTasks.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Fix Tasks ({review.FixTasks.Count}):[/]");

            foreach (var task in review.FixTasks.OrderBy(t => t.Priority))
            {
                var tree = new Tree($"[bold]P{task.Priority}[/] {Markup.Escape(task.Title)}");
                tree.AddNode($"[dim]{Markup.Escape(task.Description)}[/]");
                tree.AddNode($"Files: {Markup.Escape(string.Join(", ", task.AffectedFiles))}");
                AnsiConsole.Write(tree);
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No fix tasks needed.[/]");
        }
    }

    private static string BuildPrompt(StructuredReviewSettings settings)
    {
        // If the user supplied an explicit prompt, use it as-is.
        var customPrompt = ResolveCustomPrompt(settings);
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
