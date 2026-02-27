using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Approval handler that prompts on the console for each request.
/// </summary>
public sealed class PromptConsoleApprovalHandler : IAppServerApprovalHandler
{
    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        var (acceptDecision, declineDecision) = method switch
        {
            "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" => ("accept", "decline"),
            "execCommandApproval" or "applyPatchApproval" => ("approved", "denied"),
            _ => throw new InvalidOperationException($"Unknown approval request method '{method}'."),
        };

        Console.Error.WriteLine($"Approval request: {method}");
        if (@params is { } p)
        {
            Console.Error.WriteLine(p.ValueKind == JsonValueKind.Undefined ? "(no params)" : p.ToString());
        }

        Console.Error.Write("Approve? [y/N]: ");
        var answer = Console.ReadLine();
        var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);

        var decision = approved ? acceptDecision : declineDecision;
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { decision }));
    }
}

