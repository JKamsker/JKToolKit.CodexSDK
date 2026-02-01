using JKToolKit.CodexSDK.Public;

namespace JKToolKit.CodexSDK.Demo.Review;

internal static class Program
{
    private sealed record CliOptions(
        string WorkingDirectory,
        string? CodexExecutablePath,
        string? CommitSha,
        string? BaseBranch,
        bool Uncommitted,
        string? Title,
        string? Prompt,
        IReadOnlyList<string> AdditionalOptions);

    private static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            return 0;
        }

        using var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownCts.Cancel();
        };

        var clientOptions = new CodexClientOptions
        {
            CodexExecutablePath = options.CodexExecutablePath
        };

        await using var client = new CodexClient(clientOptions);

        try
        {
            var reviewOptions = new CodexReviewOptions(options.WorkingDirectory)
            {
                CodexBinaryPath = options.CodexExecutablePath,
                CommitSha = options.CommitSha,
                BaseBranch = options.BaseBranch,
                Uncommitted = options.Uncommitted,
                Title = options.Title,
                Prompt = options.Prompt,
                AdditionalOptions = options.AdditionalOptions
            };

            var result = await client.ReviewAsync(reviewOptions, Console.Out, Console.Error, shutdownCts.Token);

            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static CliOptions? ParseArgs(string[] args)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        string? codexExecutablePath = null;
        string? commitSha = null;
        string? baseBranch = null;
        var uncommitted = false;
        string? title = null;
        string? prompt = null;
        var additional = new List<string>();
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    PrintHelp();
                    return null;
                case "-C" or "--cd":
                    workingDirectory = RequireValue(args, ++i, arg);
                    break;
                case "--codex-path":
                    codexExecutablePath = RequireValue(args, ++i, arg);
                    break;
                case "--commit":
                    commitSha = RequireValue(args, ++i, arg);
                    break;
                case "--base":
                    baseBranch = RequireValue(args, ++i, arg);
                    break;
                case "--uncommitted":
                    uncommitted = true;
                    break;
                case "--title":
                    title = RequireValue(args, ++i, arg);
                    break;
                case "-c" or "--config":
                {
                    var kv = RequireValue(args, ++i, arg);
                    additional.Add("--config");
                    additional.Add(kv);
                    break;
                }
                case "--enable":
                {
                    var feature = RequireValue(args, ++i, arg);
                    additional.Add("--enable");
                    additional.Add(feature);
                    break;
                }
                case "--disable":
                {
                    var feature = RequireValue(args, ++i, arg);
                    additional.Add("--disable");
                    additional.Add(feature);
                    break;
                }
                case "--prompt":
                    prompt = RequireValue(args, ++i, arg);
                    break;
                case "--":
                    for (i = i + 1; i < args.Length; i++)
                    {
                        additional.Add(args[i]);
                    }
                    i = args.Length;
                    break;
                default:
                    positional.Add(arg);
                    break;
            }
        }

        if (prompt is not null && positional.Count > 0)
        {
            throw new ArgumentException("Specify prompt either via positional argument(s) or --prompt, not both.");
        }

        if (prompt is null && positional.Count > 0)
        {
            prompt = string.Join(" ", positional);
        }

        if (string.Equals(prompt, "-", StringComparison.Ordinal))
        {
            prompt = Console.In.ReadToEnd();
        }

        return new CliOptions(
            WorkingDirectory: workingDirectory,
            CodexExecutablePath: codexExecutablePath,
            CommitSha: commitSha,
            BaseBranch: baseBranch,
            Uncommitted: uncommitted,
            Title: title,
            Prompt: string.IsNullOrWhiteSpace(prompt) ? null : prompt,
            AdditionalOptions: additional);
    }

    private static string RequireValue(string[] args, int index, string name)
    {
        if (index >= args.Length)
        {
            throw new ArgumentException($"Missing value for {name}");
        }

        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
JKToolKit.CodexSDK.Demo.Review

Usage:
  dotnet run --project src/JKToolKit.CodexSDK.Demo.Review -- [OPTIONS] [PROMPT]

Options:
  -C, --cd <DIR>          Repository working directory (default: current dir)
      --commit <SHA>      Review changes introduced by a commit
      --base <BRANCH>     Review changes against the given base branch
      --uncommitted       Review staged, unstaged, and untracked changes
      --title <TITLE>     Optional title displayed in review summary
      --prompt <PROMPT>   Custom review instructions (use '-' to read from stdin)
  -c, --config <k=v>      Forward to `codex review --config`
      --enable <FEATURE>  Forward to `codex review --enable`
      --disable <FEATURE> Forward to `codex review --disable`
      --codex-path <PATH> Override Codex executable path
      --                 Forward remaining args as additional options
  -h, --help              Show help

Arguments:
  PROMPT  Custom review instructions (use '-' to read from stdin)
""");
    }
}
