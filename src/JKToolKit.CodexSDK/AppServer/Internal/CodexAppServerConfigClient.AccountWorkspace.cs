using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
    public async Task<AccountRateLimitResetCreditConsumeResult> ConsumeAccountRateLimitResetCreditAsync(
        AccountRateLimitResetCreditConsumeOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.IdempotencyKey))
            throw new ArgumentException("IdempotencyKey cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "account/rateLimitResetCredit/consume",
            new { options.IdempotencyKey },
            ct);

        return new AccountRateLimitResetCreditConsumeResult
        {
            Outcome = CodexAppServerClientJson.GetRequiredString(
                result,
                "outcome",
                "account/rateLimitResetCredit/consume response"),
            Raw = result
        };
    }

    public async Task<WorkspaceMessagesReadResult> ReadWorkspaceMessagesAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync(
            "account/workspaceMessages/read",
            null,
            ct);

        return new WorkspaceMessagesReadResult
        {
            FeatureEnabled = CodexAppServerClientJson.GetRequiredBool(
                result,
                "featureEnabled",
                "account/workspaceMessages/read response"),
            Messages = ParseWorkspaceMessages(result),
            Raw = result
        };
    }

    private static IReadOnlyList<WorkspaceMessage> ParseWorkspaceMessages(JsonElement result)
    {
        var messagesArray = CodexAppServerClientJson.TryGetArray(result, "messages")
            ?? throw new InvalidOperationException("account/workspaceMessages/read response missing required array property 'messages'.");

        var messages = new List<WorkspaceMessage>();
        foreach (var item in messagesArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("account/workspaceMessages/read messages[] entries must be objects.");
            }

            messages.Add(new WorkspaceMessage
            {
                MessageId = CodexAppServerClientJson.GetRequiredString(item, "messageId", "account/workspaceMessages/read messages[]"),
                MessageType = CodexAppServerClientJson.GetRequiredString(item, "messageType", "account/workspaceMessages/read messages[]"),
                MessageBody = CodexAppServerClientJson.GetRequiredString(item, "messageBody", "account/workspaceMessages/read messages[]"),
                CreatedAt = CodexAppServerClientJson.GetInt64OrNull(item, "createdAt"),
                ArchivedAt = CodexAppServerClientJson.GetInt64OrNull(item, "archivedAt"),
                Raw = item.Clone()
            });
        }

        return messages;
    }
}
