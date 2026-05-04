using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentToolMapper
{
    public static IEnumerable<AIFunction> GetAIFunctions(
        IReadOnlyList<AITool>? configuredTools,
        AgentRunOptions? runOptions)
    {
        foreach (var tool in EnumerateTools(configuredTools, runOptions))
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

    public static bool HasRunTools(AgentRunOptions? runOptions)
    {
        return runOptions switch
        {
            CodexAgentRunOptions { Tools.Count: > 0 } => true,
            ChatClientAgentRunOptions { ChatOptions.Tools.Count: > 0 } => true,
            _ => false
        };
    }

    private static IEnumerable<AITool> EnumerateTools(
        IReadOnlyList<AITool>? configuredTools,
        AgentRunOptions? runOptions)
    {
        if (configuredTools is not null)
        {
            foreach (var tool in configuredTools)
            {
                yield return tool;
            }
        }

        foreach (var tool in GetRunTools(runOptions))
        {
            yield return tool;
        }
    }

    private static IEnumerable<AITool> GetRunTools(AgentRunOptions? runOptions)
    {
        return runOptions switch
        {
            CodexAgentRunOptions { Tools: { } tools } => tools,
            ChatClientAgentRunOptions { ChatOptions.Tools: { } tools } => tools,
            _ => []
        };
    }
}
