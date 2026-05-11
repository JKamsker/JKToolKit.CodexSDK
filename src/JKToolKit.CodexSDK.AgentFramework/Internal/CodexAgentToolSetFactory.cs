using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentToolSetFactory
{
    public static AgentFrameworkCodexToolSet Create(
        CodexAIAgentOptions options,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions,
        IReadOnlyList<AITool>? configuredTools,
        IReadOnlyList<AITool>? codexRunConfigurationTools,
        CodexAgentSession session)
    {
        var functions = CodexAgentToolMapper.GetAIFunctions(
            configuredTools,
            codexRunConfigurationTools,
            runOptions,
            chatOptions).ToArray();
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            functions,
            new AgentFrameworkCodexToolAdapterOptions
            {
                FunctionInvocationServices = CodexAgentOptionsMapper.GetFunctionInvocationServices(options, runOptions),
                ToolApprovalHandler = CodexAgentOptionsMapper.GetToolApprovalHandler(options, runOptions),
                SafetyOptions = options.SafetyOptions
            });

        ValidateResumeToolSchema(
            session,
            CodexAgentToolMapper.HasRunTools(runOptions, chatOptions),
            toolSet.ToolSchemaHash);
        return toolSet;
    }

    private static void ValidateResumeToolSchema(
        CodexAgentSession session,
        bool hasRunTools,
        string? toolSchemaHash)
    {
        if (session.ThreadId is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(session.ToolSchemaHash))
        {
            if (!string.Equals(session.ToolSchemaHash, toolSchemaHash, StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    "Codex dynamic tools are thread-scoped, and the resumed AgentSession has a different tool schema hash. " +
                    "Create a new AgentSession to use a different tool set.");
            }

            return;
        }

        if (hasRunTools)
        {
            throw new NotSupportedException(
                "Codex dynamic tools are configured when the Codex thread is created. Create a new AgentSession to use different per-run tools.");
        }
    }
}
