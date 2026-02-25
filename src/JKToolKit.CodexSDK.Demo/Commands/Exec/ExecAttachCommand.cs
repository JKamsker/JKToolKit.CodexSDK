using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.Exec;

public sealed class ExecAttachCommand : AsyncCommand<ExecAttachSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ExecAttachSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.LogFilePath))
        {
            Console.Error.WriteLine("Missing required option: --log <PATH>");
            return 1;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        var ct = cts.Token;

        try
        {
            await using var sdk = CodexSdk.Create(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            });

            await using var session = await sdk.Exec.AttachToLogAsync(settings.LogFilePath, ct);
            Console.WriteLine($"Attached: {session.Info.Id.Value}");
            Console.WriteLine($"Log     : {session.Info.LogPath}");
            Console.WriteLine($"Created : {session.Info.CreatedAt:O}");
            Console.WriteLine();

            var options = EventStreamOptions.Default with { Follow = settings.Follow };
            await foreach (var evt in session.GetEventsAsync(options, ct))
            {
                if (evt is AgentMessageEvent agent)
                {
                    Console.WriteLine(agent.Text);
                }
                else
                {
                    Console.WriteLine(evt.Type);
                }
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
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
