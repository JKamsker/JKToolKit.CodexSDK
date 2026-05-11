using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentSessionStateUpdater
{
    public static void CaptureThreadCreation(
        CodexAgentSession session,
        AgentFrameworkCodexToolSet toolSet,
        CodexAIAgentOptions options,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        session.ToolSchemaHash = toolSet.ToolSchemaHash;
        session.Model = CodexAgentOptionsMapper.GetModel(options, runOptions, chatOptions);
        session.Cwd = CodexAgentOptionsMapper.GetCwd(options, runOptions);
        session.ApprovalPolicy = CodexAgentOptionsMapper.GetApprovalPolicy(options, runOptions);
        session.Sandbox = CodexAgentOptionsMapper.GetSandbox(options, runOptions);
        session.CreatedAt = DateTimeOffset.UtcNow;
    }
}
