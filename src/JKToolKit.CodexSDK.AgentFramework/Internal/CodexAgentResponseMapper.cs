using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentResponseMapper
{
    public static async IAsyncEnumerable<AgentResponseUpdate> StreamUpdatesAsync(
        CodexTurnHandle turn,
        string agentId,
        string authorName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var ev in turn.Events(cancellationToken).ConfigureAwait(false))
        {
            switch (ev)
            {
                case AgentMessageDeltaNotification delta:
                    yield return new AgentResponseUpdate(ChatRole.Assistant, delta.Delta)
                    {
                        AgentId = agentId,
                        AuthorName = authorName,
                        ResponseId = turn.TurnId,
                        MessageId = delta.ItemId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    break;
                case ErrorNotification error:
                    yield return new AgentResponseUpdate(ChatRole.Assistant, [new ErrorContent(error.Error.GetRawText())])
                    {
                        AgentId = agentId,
                        AuthorName = authorName,
                        ResponseId = turn.TurnId
                    };
                    break;
            }
        }

        var completed = await turn.Completion.ConfigureAwait(false);
        if (!string.Equals(completed.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
            completed.Error is { } completionError)
        {
            yield return new AgentResponseUpdate(ChatRole.Assistant, [new ErrorContent(completionError.GetRawText())])
            {
                AgentId = agentId,
                AuthorName = authorName,
                ResponseId = turn.TurnId,
                FinishReason = ChatFinishReason.Stop
            };
        }
    }
}
