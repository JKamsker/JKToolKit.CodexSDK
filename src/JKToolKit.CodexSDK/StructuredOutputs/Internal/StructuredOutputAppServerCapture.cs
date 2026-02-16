using System.Text;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.StructuredOutputs.Internal;

internal static class StructuredOutputAppServerCapture
{
    internal static async Task<string> CaptureAppServerFinalTextAsync(CodexTurnHandle turn, CancellationToken ct)
    {
        var deltas = new StringBuilder();
        string? fullText = null;
        var done = false;

        await foreach (var evt in turn.Events(ct).ConfigureAwait(false))
        {
            switch (evt)
            {
                case AgentMessageDeltaNotification d:
                    deltas.Append(d.Delta);
                    break;
                case ItemCompletedNotification ic when string.Equals(ic.ItemType, "agentMessage", StringComparison.Ordinal):
                    if (ic.Item.ValueKind == JsonValueKind.Object &&
                        ic.Item.TryGetProperty("text", out var t) &&
                        t.ValueKind == JsonValueKind.String)
                    {
                        fullText = t.GetString();
                    }
                    break;
                case TurnCompletedNotification:
                    done = true;
                    break;
            }

            if (done)
            {
                break;
            }
        }

        var raw = fullText ?? deltas.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new CodexStructuredOutputParseException(
                message: "Codex did not emit a final agent message to parse as structured output.",
                rawText: raw,
                extractedJson: null,
                innerException: new InvalidOperationException("No final message."));
        }

        return raw;
    }
}

