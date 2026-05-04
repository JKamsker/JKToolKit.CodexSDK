using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentOptionsMapper
{
    public static string? GetModel(CodexAIAgentOptions options, AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return GetRunModel(runOptions, chatOptions) ?? options.Model;
    }

    public static string? GetRunModel(AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Model
            ?? runOptions.GetCodexConfiguration()?.Model
            ?? chatOptions?.ModelId;
    }

    public static string? GetCwd(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunCwd(runOptions) ?? options.Cwd;
    }

    public static string? GetRunCwd(AgentRunOptions? runOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Cwd
            ?? runOptions.GetCodexConfiguration()?.Cwd;
    }

    public static string? GetInstructions(CodexAIAgentOptions options, ChatOptions? chatOptions)
    {
        return chatOptions?.Instructions ?? options.Instructions;
    }

    public static CodexApprovalPolicy? GetApprovalPolicy(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunApprovalPolicy(runOptions) ?? options.ApprovalPolicy;
    }

    public static CodexApprovalPolicy? GetRunApprovalPolicy(AgentRunOptions? runOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.ApprovalPolicy
            ?? runOptions.GetCodexConfiguration()?.ApprovalPolicy;
    }

    public static CodexSandboxMode? GetSandbox(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return GetRunSandbox(runOptions) ?? options.Sandbox;
    }

    public static CodexSandboxMode? GetRunSandbox(AgentRunOptions? runOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Sandbox
            ?? runOptions.GetCodexConfiguration()?.Sandbox;
    }

    public static CodexReasoningEffort? GetEffort(CodexAIAgentOptions options, AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return GetRunEffort(runOptions, chatOptions) ?? options.Effort;
    }

    public static CodexReasoningEffort? GetRunEffort(AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Effort
            ?? runOptions.GetCodexConfiguration()?.Effort
            ?? MapReasoningEffort(chatOptions?.Reasoning?.Effort);
    }

    public static string? GetSummary(CodexAIAgentOptions options, AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return GetRunSummary(runOptions, chatOptions) ?? options.Summary;
    }

    public static string? GetRunSummary(AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.Summary
            ?? runOptions.GetCodexConfiguration()?.Summary
            ?? MapReasoningOutput(chatOptions?.Reasoning?.Output);
    }

    public static JsonElement? GetOutputSchema(AgentRunOptions? runOptions, ChatOptions? chatOptions)
    {
        var responseFormat = chatOptions?.ResponseFormat ?? runOptions?.ResponseFormat;

        return responseFormat is ChatResponseFormatJson { Schema: { } schema } ? schema.Clone() : null;
    }

    public static IServiceProvider? GetFunctionInvocationServices(CodexAIAgentOptions options, AgentRunOptions? runOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.FunctionInvocationServices
            ?? runOptions.GetCodexConfiguration()?.FunctionInvocationServices
            ?? options.FunctionInvocationServices;
    }

    public static Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? GetToolApprovalHandler(
        CodexAIAgentOptions options,
        AgentRunOptions? runOptions)
    {
        return (runOptions as CodexAgentRunOptions)?.ToolApprovalHandler
            ?? runOptions.GetCodexConfiguration()?.ToolApprovalHandler
            ?? options.ToolApprovalHandler;
    }

    public static void ConfigureRunTurn(TurnStartOptions turnOptions, AgentRunOptions? runOptions)
    {
        runOptions.GetCodexConfiguration()?.ConfigureTurn?.Invoke(turnOptions);
        (runOptions as CodexAgentRunOptions)?.ConfigureTurn?.Invoke(turnOptions);
    }

    private static CodexReasoningEffort? MapReasoningEffort(ReasoningEffort? effort)
    {
        return effort switch
        {
            ReasoningEffort.None => CodexReasoningEffort.None,
            ReasoningEffort.Low => CodexReasoningEffort.Low,
            ReasoningEffort.Medium => CodexReasoningEffort.Medium,
            ReasoningEffort.High => CodexReasoningEffort.High,
            ReasoningEffort.ExtraHigh => CodexReasoningEffort.XHigh,
            _ => (CodexReasoningEffort?)null
        };
    }

    private static string? MapReasoningOutput(ReasoningOutput? output)
    {
        return output switch
        {
            ReasoningOutput.None => "none",
            ReasoningOutput.Summary => "auto",
            ReasoningOutput.Full => "detailed",
            _ => null
        };
    }
}
