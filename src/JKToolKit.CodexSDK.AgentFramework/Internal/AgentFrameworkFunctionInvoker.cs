using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class AgentFrameworkFunctionInvoker : FunctionInvokingChatClient
{
    private AgentFrameworkFunctionInvoker()
        : base(CodexAgentNoOpChatClient.Instance)
    {
    }

    public static async ValueTask<object?> InvokeAsync(
        AIFunction function,
        AIFunctionArguments arguments,
        FunctionCallContent callContent,
        CancellationToken cancellationToken)
    {
        var previousContext = CurrentContext;
        CurrentContext = new FunctionInvocationContext
        {
            Function = function,
            Arguments = arguments,
            CallContent = callContent,
            FunctionCallIndex = 0,
            FunctionCount = 1
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
}
