using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecListSessionsCommand : AsyncCommand<ExecListSessionsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ExecListSessionsSettings settings, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var sessionsRoot =
            settings.SessionsRoot ??
            (!string.IsNullOrWhiteSpace(settings.CodexHomeDirectory)
                ? Path.Combine(settings.CodexHomeDirectory, "sessions")
                : DefaultSessionsRoot());

        var model = string.IsNullOrWhiteSpace(settings.Model) ? (CodexModel?)null : CodexModel.Parse(settings.Model);

        var limit = settings.Limit <= 0 ? 25 : settings.Limit;

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureExec(o => o.SessionsRootDirectory = sessionsRoot);
        });

        try
        {
            var filter = new SessionFilter(
                WorkingDirectory: string.IsNullOrWhiteSpace(settings.WorkingDirectory) ? null : settings.WorkingDirectory,
                Model: model,
                SessionIdPattern: string.IsNullOrWhiteSpace(settings.SessionIdPattern) ? null : settings.SessionIdPattern);

            var count = 0;
            await foreach (var session in sdk.Exec.ListSessionsAsync(filter, ct))
            {
                Console.WriteLine($"{session.CreatedAt:O}  {session.Id.Value}  {session.LogPath}");

                count++;
                if (count >= limit)
                {
                    break;
                }
            }

            if (count == 0)
            {
                Console.WriteLine("No sessions found.");
            }

            return 0;
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

    private static string DefaultSessionsRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");
}

