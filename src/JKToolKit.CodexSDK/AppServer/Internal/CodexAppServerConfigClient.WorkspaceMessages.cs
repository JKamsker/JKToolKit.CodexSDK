using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
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

    private static IReadOnlyList<WorkspaceMessageInfo> ParseWorkspaceMessages(JsonElement result)
    {
        var messagesArray = CodexAppServerClientJson.TryGetArray(result, "messages")
            ?? throw new InvalidOperationException(
                "account/workspaceMessages/read response missing required array property 'messages'.");

        var messages = new List<WorkspaceMessageInfo>();
        foreach (var item in messagesArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("account/workspaceMessages/read messages[] entries must be objects.");
            }

            var messageType = CodexAppServerClientJson.GetRequiredString(
                item,
                "messageType",
                "account/workspaceMessages/read messages[]");

            messages.Add(new WorkspaceMessageInfo
            {
                MessageId = CodexAppServerClientJson.GetRequiredString(
                    item,
                    "messageId",
                    "account/workspaceMessages/read messages[]"),
                MessageType = messageType,
                MessageKind = ParseWorkspaceMessageKind(messageType),
                MessageBody = CodexAppServerClientJson.GetRequiredString(
                    item,
                    "messageBody",
                    "account/workspaceMessages/read messages[]"),
                CreatedAt = CodexAppServerClientJson.GetInt64OrNull(item, "createdAt"),
                ArchivedAt = CodexAppServerClientJson.GetInt64OrNull(item, "archivedAt"),
                Raw = item.Clone()
            });
        }

        return messages;
    }

    private static WorkspaceMessageKind ParseWorkspaceMessageKind(string value) =>
        value switch
        {
            "headline" => WorkspaceMessageKind.Headline,
            "announcement" => WorkspaceMessageKind.Announcement,
            _ => WorkspaceMessageKind.Unknown
        };
}
