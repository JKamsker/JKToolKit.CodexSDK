using System.Text;
using System.Text.Json;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerOutputSchema;

public sealed class AppServerOutputSchemaCommand : AsyncCommand<AppServerOutputSchemaSettings>
{
    private const string SchemaJson =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "answer": { "type": "string" }
          },
          "required": ["answer"]
        }
        """;

    public override async Task<int> ExecuteAsync(CommandContext context, AppServerOutputSchemaSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

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

        var schema = JsonDocument.Parse(SchemaJson).RootElement.Clone();

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o => o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK OutputSchema Demo", "1.0.0"));
        });

        try
        {
            await using var codex = await sdk.AppServer.StartAsync(ct);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = CodexModel.Gpt52Codex,
                Cwd = repoPath,
                ApprovalPolicy = CodexApprovalPolicy.Never,
                Sandbox = CodexSandboxMode.WorkspaceWrite
            }, ct);

            Console.WriteLine($"Thread: {thread.Id}");
            Console.WriteLine($"> {settings.Prompt}");
            Console.WriteLine();

            var sb = new StringBuilder();

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text(settings.Prompt)],
                OutputSchema = schema
            }, ct);

            await foreach (var ev in turn.Events(ct))
            {
                if (ev is AgentMessageDeltaNotification delta)
                {
                    sb.Append(delta.Delta);
                    Console.Write(delta.Delta);
                }
            }

            var completed = await turn.Completion;
            Console.WriteLine($"\nDone: {completed.Status}");

            var text = sb.ToString().Trim();
            if (!TryParseJsonObject(text, out var obj))
            {
                Console.Error.WriteLine("\nFailed to parse JSON object from output (OutputSchema may not have been respected).");
                Console.Error.WriteLine($"Raw output:\n{text}");
                return 1;
            }

            if (!obj.TryGetProperty("answer", out var answerProp) || answerProp.ValueKind != JsonValueKind.String)
            {
                Console.Error.WriteLine("Parsed JSON did not contain required string property 'answer'.");
                return 1;
            }

            Console.WriteLine($"Parsed answer: {answerProp.GetString()}");
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
    }

    private static bool TryParseJsonObject(string text, out JsonElement obj)
    {
        obj = default;

        try
        {
            var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            obj = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
