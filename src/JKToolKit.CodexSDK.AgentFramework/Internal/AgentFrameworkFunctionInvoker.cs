using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class AgentFrameworkFunctionInvoker : FunctionInvokingChatClient
{
    private static readonly AsyncLocal<ChatOptions?> EffectiveChatOptions = new();

    private AgentFrameworkFunctionInvoker()
        : base(CodexAgentNoOpChatClient.Instance)
    {
    }

    public static IDisposable PushEffectiveChatOptions(ChatOptions? chatOptions)
    {
        var previous = EffectiveChatOptions.Value;
        EffectiveChatOptions.Value = chatOptions;
        return new EffectiveChatOptionsScope(previous);
    }

    public static async ValueTask<object?> InvokeAsync(
        AIFunction function,
        AIFunctionArguments arguments,
        FunctionCallContent callContent,
        CancellationToken cancellationToken)
    {
        var previousContext = CurrentContext;
        var runContext = AIAgent.CurrentRunContext;
        CurrentContext = new FunctionInvocationContext
        {
            Function = function,
            Arguments = arguments,
            CallContent = callContent,
            Messages = runContext?.RequestMessages.ToArray() ?? [],
            Options = EffectiveChatOptions.Value ?? (runContext?.RunOptions as ChatClientAgentRunOptions)?.ChatOptions,
            Iteration = 1,
            FunctionCallIndex = 0,
            FunctionCount = 1,
            IsStreaming = true
        };

        try
        {
            return await function.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CurrentContext = previousContext;
        }
    }

    private sealed class EffectiveChatOptionsScope(ChatOptions? previous) : IDisposable
    {
        public void Dispose()
        {
            EffectiveChatOptions.Value = previous;
        }
    }
}
