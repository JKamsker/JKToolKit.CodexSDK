using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerThreadAttachmentParsers
{
    public static ThreadAttachmentAddResult ParseAddResult(JsonElement result)
    {
        var attachment = CodexAppServerClientJson.TryGetObject(result, "attachment")
            ?? throw new InvalidOperationException("Missing required property 'attachment' on thread/attachment/add response.");

        return new ThreadAttachmentAddResult
        {
            Outcome = ParseAddOutcome(CodexAppServerClientJson.GetStringOrNull(result, "outcome")),
            Attachment = ParseAttachment(attachment),
            Raw = result
        };
    }

    public static ThreadAttachmentListPage ParseListPage(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, "data")
            ?? throw new InvalidOperationException("Missing required property 'data' on thread/attachment/list response.");
        var attachments = new List<ThreadAttachmentInfo>();

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("thread/attachment/list response contains a non-object entry in 'data'.");
            }

            attachments.Add(ParseAttachment(item));
        }

        return new ThreadAttachmentListPage
        {
            Attachments = attachments,
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
            Raw = result
        };
    }

    private static ThreadAttachmentInfo ParseAttachment(JsonElement attachment)
    {
        if (!attachment.TryGetProperty("payload", out var payload))
        {
            throw new InvalidOperationException("Missing required property 'payload' on thread attachment.");
        }

        return new ThreadAttachmentInfo
        {
            Id = CodexAppServerClientJson.GetRequiredString(attachment, "id", "thread attachment"),
            AttachmentType = CodexAppServerClientJson.GetRequiredString(attachment, "attachmentType", "thread attachment"),
            IdentityKey = CodexAppServerClientJson.GetRequiredString(attachment, "identityKey", "thread attachment"),
            Payload = payload.Clone(),
            CreatedAt = CodexAppServerClientJson.GetRequiredInt64(attachment, "createdAt", "thread attachment"),
            Raw = attachment.Clone()
        };
    }

    private static ThreadAttachmentAddOutcome ParseAddOutcome(string? outcome) =>
        outcome switch
        {
            "created" => ThreadAttachmentAddOutcome.Created,
            "existing" => ThreadAttachmentAddOutcome.Existing,
            _ => ThreadAttachmentAddOutcome.Unknown
        };
}
