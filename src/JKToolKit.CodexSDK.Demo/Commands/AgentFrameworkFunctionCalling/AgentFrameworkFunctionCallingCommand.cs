using System.Text.Json;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Demo.Commands.Common.Pizza;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
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
            var agent = new CodexAgentClient(builder =>
            {
                builder.CodexExecutablePath = settings.CodexExecutablePath;
                builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            }).AsAIAgent(
                model: model?.Value,
                instructions: "Use the pizza tools for menu, cart, and checkout state. Do not invent cart contents.",
                name: "CodexPizzaAgent",
                tools: functions);

            PrintTools(functions);
            var session = await agent.CreateSessionAsync(cts.Token);
            var runOptions = new CodexAgentRunOptions
            {
                Cwd = repoPath,
                ApprovalPolicy = approvalPolicy,
                Sandbox = sandbox
            };

            await foreach (var update in agent.RunStreamingAsync(GetPrompt(settings), session, runOptions, cts.Token))
            {
                Console.Write(update.Text);
            }

            Console.WriteLine("\nDone");
            if (session is CodexAgentSession codexSession)
            {
                Console.WriteLine($"Thread: {codexSession.ThreadId}");
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

    private static void PrintTools(IReadOnlyList<AIFunction> functions)
    {
        Console.WriteLine("Dynamic tools from Microsoft Agent Framework functions:");
        foreach (var function in functions)
        {
            Console.WriteLine($"- {function.Name}");
        }

        Console.WriteLine();
    }
}
