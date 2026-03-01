using System.Text;
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

        if (method == "item/tool/call")
        {
            return ValueTask.FromResult(HandleDynamicToolCall(@params));
        }

        if (method == "account/chatgptAuthTokens/refresh")
        {
            return ValueTask.FromResult(HandleChatgptAuthTokensRefresh(@params));
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

    private static JsonElement HandleDynamicToolCall(JsonElement? @params)
    {
        if (@params is not { } raw)
        {
            throw new InvalidOperationException("item/tool/call missing params.");
        }

        var request = raw.Deserialize<DynamicToolCallParams>(SerializerOptions) ??
                      throw new InvalidOperationException("Failed to deserialize item/tool/call params.");

        Console.Error.WriteLine("Server request: item/tool/call");
        Console.Error.WriteLine($"threadId={request.ThreadId} turnId={request.TurnId} callId={request.CallId} tool={request.Tool}");
        Console.Error.WriteLine(request.Arguments.ValueKind == JsonValueKind.Undefined ? "(no arguments)" : request.Arguments.ToString());

        Console.Error.Write("Success? [Y/n]: ");
        var answer = Console.ReadLine();
        var success = string.IsNullOrWhiteSpace(answer) ||
                      string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);

        Console.Error.Write("Output text (optional): ");
        var text = Console.ReadLine();

        var contentItems = new List<DynamicToolCallOutputContentItem>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            contentItems.Add(DynamicToolCallOutputContentItem.InputText(text));
        }

        return JsonSerializer.SerializeToElement(
            new DynamicToolCallResponse
            {
                Success = success,
                ContentItems = contentItems
            },
            SerializerOptions);
    }

    private static JsonElement HandleChatgptAuthTokensRefresh(JsonElement? @params)
    {
        if (@params is not { } raw)
        {
            throw new InvalidOperationException("account/chatgptAuthTokens/refresh missing params.");
        }

        var request = raw.Deserialize<ChatgptAuthTokensRefreshParams>(SerializerOptions) ??
                      throw new InvalidOperationException("Failed to deserialize account/chatgptAuthTokens/refresh params.");

        Console.Error.WriteLine("Server request: account/chatgptAuthTokens/refresh");
        Console.Error.WriteLine($"reason={request.Reason} previousAccountId={request.PreviousAccountId ?? "n/a"}");
        Console.Error.WriteLine("Provide refreshed tokens for Codex to continue.");

        Console.Error.Write("ChatGPT account id: ");
        var accountId = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new InvalidOperationException("ChatGPT account id is required.");
        }

        Console.Error.Write("ChatGPT plan type (optional): ");
        var planType = Console.ReadLine();

        Console.Error.Write("Access token (input hidden): ");
        var accessToken = ReadSecretLine();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Access token is required.");
        }

        return JsonSerializer.SerializeToElement(
            new ChatgptAuthTokensRefreshResponse
            {
                AccessToken = accessToken,
                ChatgptAccountId = accountId,
                ChatgptPlanType = string.IsNullOrWhiteSpace(planType) ? null : planType
            },
            SerializerOptions);
    }

    private static string ReadSecretLine()
    {
        var sb = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return sb.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Length--;
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
            }
        }
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

            Console.Error.Write(q.IsSecret ? "Answer (input hidden, comma-separated): " : "Answer (comma-separated): ");
            var line = q.IsSecret ? ReadSecretLine() : (Console.ReadLine() ?? string.Empty);
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

