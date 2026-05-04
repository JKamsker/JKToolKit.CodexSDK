namespace JKToolKit.CodexSDK.AgentFramework.Tools;

/// <summary>
/// Represents a host approval decision for an Agent Framework function call requested by Codex.
/// </summary>
public sealed class AgentFrameworkToolApprovalResponse
{
    private AgentFrameworkToolApprovalResponse(bool approved, string? reason)
    {
        Approved = approved;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the tool call is approved.
    /// </summary>
    public bool Approved { get; }

    /// <summary>
    /// Gets an optional reason for approval or rejection.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates an approval response.
    /// </summary>
    public static AgentFrameworkToolApprovalResponse Approve(string? reason = null)
    {
        return new AgentFrameworkToolApprovalResponse(approved: true, reason);
    }

    /// <summary>
    /// Creates a rejection response.
    /// </summary>
    public static AgentFrameworkToolApprovalResponse Reject(string? reason = null)
    {
        return new AgentFrameworkToolApprovalResponse(approved: false, reason);
    }
}
