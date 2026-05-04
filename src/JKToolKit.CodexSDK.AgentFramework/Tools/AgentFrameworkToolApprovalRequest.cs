using System.Text.Json;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

/// <summary>
/// Describes a Codex dynamic tool call that targets an Agent Framework function requiring host approval.
/// </summary>
public sealed class AgentFrameworkToolApprovalRequest
{
    /// <summary>
    /// Creates an approval request for an Agent Framework function call.
    /// </summary>
    public AgentFrameworkToolApprovalRequest(
        string threadId,
        string turnId,
        string callId,
        AIFunction function,
        JsonElement arguments)
    {
        ThreadId = threadId;
        TurnId = turnId;
        CallId = callId;
        Function = function;
        Arguments = arguments.Clone();
    }

    /// <summary>
    /// Gets the Codex thread id associated with this tool call.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the Codex turn id associated with this tool call.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the Codex tool call id.
    /// </summary>
    public string CallId { get; }

    /// <summary>
    /// Gets the function that would be invoked if approved.
    /// </summary>
    public AIFunction Function { get; }

    /// <summary>
    /// Gets the raw JSON arguments supplied by Codex.
    /// </summary>
    public JsonElement Arguments { get; }

    /// <summary>
    /// Gets the Agent Framework function name.
    /// </summary>
    public string ToolName => Function.Name;
}
