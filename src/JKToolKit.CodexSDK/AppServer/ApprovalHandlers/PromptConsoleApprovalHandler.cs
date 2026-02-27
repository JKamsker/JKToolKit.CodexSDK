using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

/// <summary>
/// Handler that prompts on the console for each server request.
/// </summary>
public sealed class PromptConsoleApprovalHandler : IAppServerApprovalHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
    {
        if (method == "item/tool/requestUserInput")
        {
            return ValueTask.FromResult(HandleRequestUserInput(@params));
        }

        var (acceptDecision, declineDecision) = method switch
        {
            "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" => ("accept", "decline"),
            "execCommandApproval" or "applyPatchApproval" => ("approved", "denied"),
            _ => throw new InvalidOperationException($"Unknown server request method '{method}'."),
        };

        Console.Error.WriteLine($"Server request: {method}");
        if (@params is { } p)
        {
            Console.Error.WriteLine(p.ValueKind == JsonValueKind.Undefined ? "(no params)" : p.ToString());
        }

        Console.Error.Write("Approve? [y/N]: ");
        var answer = Console.ReadLine();
        var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);

        var decision = approved ? acceptDecision : declineDecision;
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { decision }, SerializerOptions));
    }

    private static JsonElement HandleRequestUserInput(JsonElement? @params)
    {
        if (@params is not { } raw)
        {
            throw new InvalidOperationException("item/tool/requestUserInput missing params.");
        }

        var request = raw.Deserialize<ToolRequestUserInputParams>(SerializerOptions) ??
                      throw new InvalidOperationException("Failed to deserialize item/tool/requestUserInput params.");

        Console.Error.WriteLine("Server request: item/tool/requestUserInput");
        Console.Error.WriteLine($"threadId={request.ThreadId} turnId={request.TurnId} itemId={request.ItemId}");

        var answers = new Dictionary<string, ToolRequestUserInputAnswer>(StringComparer.Ordinal);

        foreach (var q in request.Questions)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{q.Header} ({q.Id})");
            Console.Error.WriteLine(q.Question);

            if (q.Options is { Count: > 0 })
            {
                for (var i = 0; i < q.Options.Count; i++)
                {
                    var opt = q.Options[i];
                    Console.Error.WriteLine($"  {i + 1}) {opt.Label} — {opt.Description}");
                }

                if (q.IsOther)
                {
                    Console.Error.WriteLine("  (Other: enter free-form text)");
                }
            }

            Console.Error.Write("Answer (comma-separated): ");
            var line = Console.ReadLine() ?? string.Empty;
            var tokens = line
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var resolved = new List<string>(tokens.Length);
            foreach (var token in tokens)
            {
                if (q.Options is { Count: > 0 } &&
                    int.TryParse(token, out var idx) &&
                    idx >= 1 &&
                    idx <= q.Options.Count)
                {
                    resolved.Add(q.Options[idx - 1].Label);
                }
                else
                {
                    resolved.Add(token);
                }
            }

            answers[q.Id] = new ToolRequestUserInputAnswer
            {
                Answers = resolved
            };
        }

        return JsonSerializer.SerializeToElement(new ToolRequestUserInputResponse { Answers = answers }, SerializerOptions);
    }
}

