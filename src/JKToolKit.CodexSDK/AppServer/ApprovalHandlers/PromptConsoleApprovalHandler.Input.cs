using System.Text;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

public sealed partial class PromptConsoleApprovalHandler
{
    private string ReadSecretLine()
    {
        var sb = new StringBuilder();

        while (true)
        {
            var key = _readKey();
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

    private static bool ReadYesNo(string prompt, bool defaultValue)
    {
        Console.Error.Write(prompt);
        var answer = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(answer))
        {
            return defaultValue;
        }

        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement PromptForAvailableDecision(IReadOnlyList<JsonElement> availableDecisions)
    {
        Console.Error.WriteLine("Available decisions:");
        for (var i = 0; i < availableDecisions.Count; i++)
        {
            Console.Error.WriteLine($"  {i + 1}) {AppServerApprovalDecisionJson.DescribeDecision(availableDecisions[i])}");
        }

        Console.Error.Write("Decision [1]: ");
        var answer = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(answer))
        {
            return availableDecisions[0].Clone();
        }

        if (int.TryParse(answer, out var index) &&
            index >= 1 &&
            index <= availableDecisions.Count)
        {
            return availableDecisions[index - 1].Clone();
        }

        throw new InvalidOperationException("Invalid approval decision selection.");
    }

    private static McpServerElicitationAction ReadElicitationAction()
    {
        Console.Error.Write("Action? [a]ccept/[d]ecline/[c]ancel (default decline): ");
        var answer = (Console.ReadLine() ?? string.Empty).Trim();

        return answer.ToLowerInvariant() switch
        {
            "a" or "accept" or "y" or "yes" => McpServerElicitationAction.Accept,
            "c" or "cancel" => McpServerElicitationAction.Cancel,
            _ => McpServerElicitationAction.Decline
        };
    }

    private static JsonElement? ReadElicitationContent(McpServerElicitationMode mode)
    {
        if (mode == McpServerElicitationMode.Url)
        {
            return null;
        }

        Console.Error.Write("Accepted form content as JSON (blank for {}): ");
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            return EmptyObject();
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Accepted elicitation content must be valid JSON.", ex);
        }
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
