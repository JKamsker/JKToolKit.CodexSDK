using System.Text.Json;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentOptionsMapper
{
    public static string? GetModel(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunModel(runOptions) ?? options.Model;
    }

    public static string? GetRunModel(AgentRunOptions? runOptions)
    {
        return runOptions switch
        {
            CodexAgentRunOptions codex => codex.Model,
            ChatClientAgentRunOptions chat => chat.ChatOptions?.ModelId,
            _ => null
        };
    }

    public static string? GetCwd(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunCwd(runOptions) ?? options.Cwd;
    }

    public static string? GetRunCwd(AgentRunOptions? runOptions)
    {
        return runOptions is CodexAgentRunOptions codex ? codex.Cwd : null;
    }

    public static string? GetInstructions(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return runOptions is ChatClientAgentRunOptions { ChatOptions.Instructions: { } instructions }
            ? instructions
            : options.Instructions;
    }

    public static CodexApprovalPolicy? GetApprovalPolicy(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunApprovalPolicy(runOptions) ?? options.ApprovalPolicy;
    }

    public static CodexApprovalPolicy? GetRunApprovalPolicy(AgentRunOptions? runOptions)
    {
        return runOptions is CodexAgentRunOptions codex ? codex.ApprovalPolicy : null;
    }

    public static CodexSandboxMode? GetSandbox(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunSandbox(runOptions) ?? options.Sandbox;
    }

    public static CodexSandboxMode? GetRunSandbox(AgentRunOptions? runOptions)
    {
        return runOptions is CodexAgentRunOptions codex ? codex.Sandbox : null;
    }

    public static JsonElement? GetOutputSchema(AgentRunOptions? runOptions)
    {
        var responseFormat = runOptions switch
        {
            ChatClientAgentRunOptions { ChatOptions.ResponseFormat: { } format } => format,
            { ResponseFormat: { } format } => format,
            _ => null
        };

        return responseFormat is ChatResponseFormatJson { Schema: { } schema } ? schema.Clone() : null;
    }
}
