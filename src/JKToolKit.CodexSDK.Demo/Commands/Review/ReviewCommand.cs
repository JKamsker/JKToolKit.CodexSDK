using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Review;

public sealed class ReviewCommand : AsyncCommand<ReviewSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ReviewSettings settings, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var workingDirectory = settings.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var prompt = ResolvePrompt(settings);

        var additional = new List<string>();
        foreach (var kv in settings.Config)
        {
            additional.Add("--config");
            additional.Add(kv);
        }

        foreach (var feature in settings.Enable)
        {
            additional.Add("--enable");
            additional.Add(feature);
        }

        foreach (var feature in settings.Disable)
        {
            additional.Add("--disable");
            additional.Add(feature);
        }

        additional.AddRange(settings.Additional);

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
        });

        try
        {
            var reviewOptions = new CodexReviewOptions(workingDirectory)
            {
                CodexBinaryPath = settings.CodexExecutablePath,
                CommitSha = settings.CommitSha,
                BaseBranch = settings.BaseBranch,
                Uncommitted = settings.Uncommitted,
                Title = settings.Title,
                Prompt = prompt,
                AdditionalOptions = additional
            };

            var result = await sdk.Exec.ReviewAsync(reviewOptions, ct);

            if (!string.IsNullOrEmpty(result.StandardOutput))
            {
                Console.Out.Write(result.StandardOutput);
            }

            if (!string.IsNullOrEmpty(result.StandardError))
            {
                Console.Error.Write(result.StandardError);
            }

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

    private static string? ResolvePrompt(ReviewSettings settings)
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
