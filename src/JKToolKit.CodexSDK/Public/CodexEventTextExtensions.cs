using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.Public;

/// <summary>
/// Convenience helpers for extracting human-readable text from Codex events.
/// </summary>
public static class CodexEventTextExtensions
{
    /// <summary>
    /// Enumerates text payload candidates from an event (assistant message text, reasoning, etc.).
    /// </summary>
    public static IEnumerable<string> EnumerateTextCandidates(this CodexEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        switch (evt)
        {
            case AgentMessageEvent msg:
                if (!string.IsNullOrWhiteSpace(msg.Text))
                    yield return msg.Text;
                yield break;

            case AgentReasoningEvent reasoning:
                if (!string.IsNullOrWhiteSpace(reasoning.Text))
                    yield return reasoning.Text;
                yield break;

            case UserMessageEvent user:
                if (!string.IsNullOrWhiteSpace(user.Text))
                    yield return user.Text;
                yield break;

            case ResponseItemEvent item:
                foreach (var text in EnumerateResponseItemTextCandidates(item))
                    yield return text;
                yield break;

            default:
                yield break;
        }
    }

    private static IEnumerable<string> EnumerateResponseItemTextCandidates(ResponseItemEvent item)
    {
        // Message payloads may be normalized by the SDK.
        if (string.Equals(item.PayloadType, "message", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Payload.MessageRole, "assistant", StringComparison.OrdinalIgnoreCase) &&
            item.Payload.MessageTextParts is { Count: > 0 } parts)
        {
            yield return string.Join("\n", parts);
        }

        // Reasoning payloads sometimes include summary text segments.
        if (string.Equals(item.PayloadType, "reasoning", StringComparison.OrdinalIgnoreCase) &&
            item.Payload.SummaryTexts is { Count: > 0 } summaries)
        {
            foreach (var s in summaries)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    yield return s;
            }
        }
    }
}

