using System.Text.Json;
using JKToolKit.CodexSDK.AgentFramework;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.AI;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AgentFrameworkFunctionCalling;

public sealed class AgentFrameworkFunctionCallingCommand : AsyncCommand<AgentFrameworkFunctionCallingSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AgentFrameworkFunctionCallingSettings settings,
        CancellationToken cancellationToken)
    {
        var repoPath = settings.RepoPath ?? Directory.GetCurrentDirectory();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            Console.CancelKeyPress += cancelHandler;

            var model = string.IsNullOrWhiteSpace(settings.Model)
                ? (CodexModel?)null
                : CodexModel.Parse(settings.Model);
            var approvalPolicy = string.IsNullOrWhiteSpace(settings.ApprovalPolicy)
                ? CodexApprovalPolicy.Never
                : CodexApprovalPolicy.Parse(settings.ApprovalPolicy);
            var sandbox = string.IsNullOrWhiteSpace(settings.Sandbox)
                ? CodexSandboxMode.ReadOnly
                : CodexSandboxMode.Parse(settings.Sandbox);

            var pizzaService = new InMemoryPizzaService();
            var functions = new OrderPizzaFunctions(pizzaService).CreateFunctions();
            var toolSet = AgentFrameworkCodexToolAdapter.Create(functions);

            await using var sdk = CodexSdk.Create(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
                builder.ConfigureAppServer(o =>
                {
                    o.ExperimentalApi = true;
                    o.ApprovalHandler = toolSet.ApprovalHandler;
                    o.DefaultClientInfo = new("ncodexsdk-agent-framework-demo", "JKToolKit.CodexSDK Agent Framework Demo", "1.0.0");
                });
            });

            await using var codex = await sdk.AppServer.StartAsync(cts.Token);
            PrintTools(functions, toolSet);

            var thread = await codex.StartThreadAsync(new ThreadStartOptions
            {
                Model = model,
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox,
                DynamicTools = toolSet.DynamicTools,
                DeveloperInstructions = "Use the pizza tools for menu, cart, and checkout state. Do not invent cart contents."
            }, cts.Token);

            await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text(GetPrompt(settings))]
            }, cts.Token);

            await foreach (var ev in turn.Events(cts.Token))
            {
                if (ev is AgentMessageDeltaNotification delta)
                {
                    Console.Write(delta.Delta);
                }

                if (ev is ErrorNotification error)
                {
                    Console.Error.WriteLine($"Turn error: {error.Error}");
                }
            }

            var completed = await turn.Completion;
            Console.WriteLine($"\nDone: {completed.Status}");
            if (completed.Error is { } completedError)
            {
                Console.Error.WriteLine($"Completion error: {completedError}");
            }

            Console.WriteLine("Final cart:");
            Console.WriteLine(JsonSerializer.Serialize(pizzaService.GetCart(), OutputJsonOptions));
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
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static string GetPrompt(AgentFrameworkFunctionCallingSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.Prompt)
            ? "Order one large pepperoni and mushroom pizza, show me the cart, then checkout."
            : settings.Prompt;
    }

    private static void PrintTools(IReadOnlyList<AIFunction> functions, AgentFrameworkCodexToolSet toolSet)
    {
        Console.WriteLine("Dynamic tools from Microsoft Agent Framework functions:");
        foreach (var tool in toolSet.DynamicTools)
        {
            var source = functions.First(function => function.Name == tool.Name);
            Console.WriteLine($"- {tool.Name} ({source.GetType().Name})");
        }

        Console.WriteLine();
    }
}
