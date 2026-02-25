using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the app-server <c>AskForApproval</c> union used by <c>approvalPolicy</c>.
/// </summary>
/// <remarks>
/// Upstream supports either a simple string policy (for example, <c>untrusted</c>) or an object form that can
/// selectively reject specific approval prompt types.
/// </remarks>
public readonly record struct CodexAskForApproval
{
    /// <summary>
    /// Gets the simple approval policy value, when using the string form.
    /// </summary>
    public CodexApprovalPolicy? Policy { get; }

    /// <summary>
    /// Gets the reject configuration, when using the object form.
    /// </summary>
    public CodexAskForApprovalReject? Reject { get; }

    private CodexAskForApproval(CodexApprovalPolicy? policy, CodexAskForApprovalReject? reject)
    {
        if (policy is null == reject is null)
            throw new ArgumentException("Specify either Policy or Reject, not both.");

        Policy = policy;
        Reject = reject;
    }

    /// <summary>
    /// Creates an <see cref="CodexAskForApproval"/> using the string policy form.
    /// </summary>
    public static CodexAskForApproval FromPolicy(CodexApprovalPolicy policy) => new(policy, reject: null);

    /// <summary>
    /// Creates an <see cref="CodexAskForApproval"/> using the object reject form.
    /// </summary>
    public static CodexAskForApproval Rejecting(CodexAskForApprovalReject reject) => new(policy: null, reject);

    /// <summary>
    /// Convenience helper for creating a reject configuration.
    /// </summary>
    public static CodexAskForApproval Rejecting(bool mcpElicitations, bool rules, bool sandboxApproval) =>
        Rejecting(new CodexAskForApprovalReject
        {
            McpElicitations = mcpElicitations,
            Rules = rules,
            SandboxApproval = sandboxApproval
        });

    internal object ToWireValue()
    {
        if (Policy is { } p)
        {
            return p.Value;
        }

        if (Reject is { } r)
        {
            return new RejectAskForApprovalWire { Reject = r };
        }

        throw new InvalidOperationException("CodexAskForApproval is not initialized.");
    }

    /// <summary>
    /// Converts a <see cref="CodexApprovalPolicy"/> to the union type.
    /// </summary>
    public static implicit operator CodexAskForApproval(CodexApprovalPolicy policy) => FromPolicy(policy);

    /// <summary>
    /// Converts a string to the union type.
    /// </summary>
    public static implicit operator CodexAskForApproval(string policy) => FromPolicy(CodexApprovalPolicy.Parse(policy));

    private sealed record class RejectAskForApprovalWire
    {
        [JsonPropertyName("reject")]
        public required CodexAskForApprovalReject Reject { get; init; }
    }
}

/// <summary>
/// Selectively rejects specific approval prompt types while still allowing others.
/// </summary>
public sealed record class CodexAskForApprovalReject
{
    /// <summary>
    /// Reject MCP elicitation approvals (for example, forms/questions).
    /// </summary>
    [JsonPropertyName("mcp_elicitations")]
    public required bool McpElicitations { get; init; }

    /// <summary>
    /// Reject "rules" approvals.
    /// </summary>
    [JsonPropertyName("rules")]
    public required bool Rules { get; init; }

    /// <summary>
    /// Reject sandbox escalation approvals (for example, requests for extra sandbox permissions).
    /// </summary>
    [JsonPropertyName("sandbox_approval")]
    public required bool SandboxApproval { get; init; }
}
