using JKToolKit.CodexSDK.AppServer;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentMessageMapper
{
    public static IReadOnlyList<TurnInputItem> ToTurnInputItems(IEnumerable<ChatMessage> messages)
    {
        var materialized = messages.ToArray();
        if (materialized.Length == 1 && materialized[0].Role == ChatRole.User)
        {
            return ToUserInputItems(materialized[0]);
        }

        var transcript = string.Join(
            Environment.NewLine + Environment.NewLine,
            materialized.Select(FormatMessage).Where(static text => !string.IsNullOrWhiteSpace(text)));

        return string.IsNullOrWhiteSpace(transcript)
            ? []
            : [TurnInputItem.Text(transcript)];
    }

    private static IReadOnlyList<TurnInputItem> ToUserInputItems(ChatMessage message)
    {
        var input = new List<TurnInputItem>();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent text when !string.IsNullOrWhiteSpace(text.Text):
                    input.Add(TurnInputItem.Text(text.Text));
                    break;
                case UriContent uri when IsImage(uri.MediaType):
                    input.Add(ToImageInput(uri.Uri));
                    break;
                case DataContent data when IsImage(data.MediaType) && !string.IsNullOrWhiteSpace(data.Uri):
                    input.Add(ToImageInput(new Uri(data.Uri)));
                    break;
            }
        }

        if (input.Count == 0 && !string.IsNullOrWhiteSpace(message.Text))
        {
            input.Add(TurnInputItem.Text(message.Text));
        }

        return input;
    }

    private static TurnInputItem ToImageInput(Uri uri)
    {
        return uri.IsFile
            ? TurnInputItem.LocalImage(uri.LocalPath)
            : TurnInputItem.ImageUrl(uri.ToString());
    }

    private static string FormatMessage(ChatMessage message)
    {
        var text = string.Join(Environment.NewLine, message.Contents.Select(FormatContent).Where(static part => part.Length > 0));
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $"{message.Role}: {text}";
    }

    private static string FormatContent(AIContent content)
    {
        return content switch
        {
            TextContent text => text.Text,
            UriContent uri => $"[uri:{uri.MediaType}] {uri.Uri}",
            DataContent { Uri: { } uri, MediaType: { } mediaType } => $"[data:{mediaType}] {uri}",
            FunctionCallContent call => $"[function_call:{call.Name}] {FormatArguments(call.Arguments)}",
            FunctionResultContent result => $"[function_result] {result.Result}",
            ToolApprovalResponseContent approval => $"[tool_approval:{approval.Approved}] {approval.Reason}",
            ErrorContent error => $"[error] {error.Message}",
            _ => content.ToString() ?? string.Empty
        };
    }

    private static string FormatArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null)
        {
            return string.Empty;
        }

        return string.Join(", ", arguments.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static bool IsImage(string? mediaType)
    {
        return mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
    }
}
