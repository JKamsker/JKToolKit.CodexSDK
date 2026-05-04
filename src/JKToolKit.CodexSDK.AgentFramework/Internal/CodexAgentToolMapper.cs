using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentToolMapper
{
    public static IEnumerable<AIFunction> GetAIFunctions(
        IReadOnlyList<AITool>? configuredTools,
        IReadOnlyList<AITool>? codexRunConfigurationTools,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        foreach (var tool in EnumerateTools(configuredTools, codexRunConfigurationTools, runOptions, chatOptions))
        {
            if (tool is AIFunction function)
            {
                yield return function;
                continue;
            }

            throw new NotSupportedException(
                $"Codex Agent Framework integration currently supports AIFunction tools only. Tool '{tool.Name}' is '{tool.GetType().Name}'.");
        }
    }

    public static bool HasRunTools(AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Tools?.Count > 0 ||
               runOptions.GetCodexConfiguration()?.Tools?.Count > 0 ||
               chatOptions?.Tools?.Count > 0;
    }

    private static IEnumerable<AITool> EnumerateTools(
        IReadOnlyList<AITool>? configuredTools,
        IReadOnlyList<AITool>? codexRunConfigurationTools,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        if (configuredTools is not null)
        {
            foreach (var tool in configuredTools)
            {
                yield return tool;
            }
        }

        foreach (var tool in GetRunTools(codexRunConfigurationTools, runOptions, chatOptions))
        {
            yield return tool;
        }
    }

    private static IEnumerable<AITool> GetRunTools(
        IReadOnlyList<AITool>? codexRunConfigurationTools,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        if (codexRunConfigurationTools is { } configuredTools)
        {
            foreach (var tool in configuredTools)
            {
                yield return tool;
            }
        }

        if (runOptions is CodexAgentRunOptions { Tools: { } codexTools })
        {
            foreach (var tool in codexTools)
            {
                yield return tool;
            }
        }

        if (chatOptions?.Tools is null)
        {
            yield break;
        }

        foreach (var tool in chatOptions.Tools)
        {
            yield return tool;
        }
    }
}
