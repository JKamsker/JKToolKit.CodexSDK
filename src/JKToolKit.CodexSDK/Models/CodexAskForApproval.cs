using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents the app-server <c>AskForApproval</c> union used by <c>approvalPolicy</c>.
/// </summary>
/// <remarks>
/// Upstream supports either a simple string policy (for example, <c>untrusted</c>) or an object form
/// (<c>{"granular":{...}}</c>) that controls approval prompt categories individually.
/// </remarks>
public readonly record struct CodexAskForApproval
{
    /// <summary>
    /// Gets the simple approval policy value, when using the string form.
    /// </summary>
    public CodexApprovalPolicy? Policy { get; }

    /// <summary>
    /// Gets the granular configuration, when using the object form.
    /// </summary>
    public CodexAskForApprovalGranular? Granular { get; }

    /// <summary>
    /// Gets a legacy subset view of the object-form configuration.
    /// </summary>
    /// <remarks>
    /// This preserves source compatibility for callers that still consume the older reject model.
    /// </remarks>
    public CodexAskForApprovalReject? Reject =>
        Granular is null
            ? null
            : new CodexAskForApprovalReject
            {
                McpElicitations = !Granular.McpElicitations,
                Rules = !Granular.Rules,
                SandboxApproval = !Granular.SandboxApproval
            };

    private CodexAskForApproval(CodexApprovalPolicy? policy, CodexAskForApprovalGranular? granular)
    {
        if (policy is null == granular is null)
            throw new ArgumentException("Specify either Policy or Granular, not both.");

        Policy = policy;
        Granular = granular;
    }

    /// <summary>
    /// Creates an <see cref="CodexAskForApproval"/> using the string policy form.
    /// </summary>
    public static CodexAskForApproval FromPolicy(CodexApprovalPolicy policy) => new(policy, granular: null);

    /// <summary>
    /// Creates an <see cref="CodexAskForApproval"/> using the object granular form.
    /// </summary>
    public static CodexAskForApproval FromGranular(CodexAskForApprovalGranular granular)
    {
        ArgumentNullException.ThrowIfNull(granular);
        return new(policy: null, granular);
    }

    /// <summary>
    /// Creates an <see cref="CodexAskForApproval"/> using the legacy reject object form.
    /// </summary>
    /// <remarks>
    /// The value is serialized as upstream granular form (<c>{"granular":{...}}</c>).
    /// </remarks>
    public static CodexAskForApproval Rejecting(CodexAskForApprovalReject reject)
    {
        ArgumentNullException.ThrowIfNull(reject);
        return FromGranular(reject.ToGranular());
    }

    /// <summary>
    /// Convenience helper for creating a legacy reject configuration.
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

        if (Granular is { } g)
        {
            return new GranularAskForApprovalWire { Granular = g };
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

    /// <summary>
    /// Converts a granular object form to the union type.
    /// </summary>
    public static implicit operator CodexAskForApproval(CodexAskForApprovalGranular granular) => FromGranular(granular);

    /// <summary>
    /// Converts a legacy reject object form to the union type.
    /// </summary>
    public static implicit operator CodexAskForApproval(CodexAskForApprovalReject reject) => Rejecting(reject);

    private sealed record class GranularAskForApprovalWire
    {
        [JsonPropertyName("granular")]
        public required CodexAskForApprovalGranular Granular { get; init; }
    }
}

/// <summary>
/// Controls approval prompt categories when using object-form approval policy.
/// </summary>
public sealed record class CodexAskForApprovalGranular
{
    /// <summary>
    /// Enable sandbox escalation approvals (for example, requests for extra sandbox permissions).
    /// </summary>
    [JsonPropertyName("sandbox_approval")]
    public required bool SandboxApproval { get; init; }

    /// <summary>
    /// Enable approvals for rules prompts.
    /// </summary>
    [JsonPropertyName("rules")]
    public required bool Rules { get; init; }

    /// <summary>
    /// Enable approvals for skill prompts.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> when omitted.
    /// </remarks>
    [JsonPropertyName("skill_approval")]
    public bool SkillApproval { get; init; }

    /// <summary>
    /// Enable approvals for the <c>request_permissions</c> tool.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> when omitted.
    /// </remarks>
    [JsonPropertyName("request_permissions")]
    public bool RequestPermissions { get; init; }

    /// <summary>
    /// Enable MCP elicitation approvals (for example, forms/questions).
    /// </summary>
    [JsonPropertyName("mcp_elicitations")]
    public required bool McpElicitations { get; init; }
}

/// <summary>
/// Legacy subset of object-form approval policy.
/// </summary>
/// <remarks>
/// This type is preserved for compatibility and maps to granular object form on serialization.
/// A value of <c>true</c> means "reject that category"; granular values are the inverse.
/// </remarks>
public sealed record class CodexAskForApprovalReject
{
    /// <summary>
    /// Reject MCP elicitation approvals (for example, forms/questions).
    /// </summary>
    [JsonPropertyName("mcp_elicitations")]
    public required bool McpElicitations { get; init; }

    /// <summary>
    /// Reject approvals for rules prompts.
    /// </summary>
    [JsonPropertyName("rules")]
    public required bool Rules { get; init; }

    /// <summary>
    /// Reject sandbox escalation approvals (for example, requests for extra sandbox permissions).
    /// </summary>
    [JsonPropertyName("sandbox_approval")]
    public required bool SandboxApproval { get; init; }

    internal CodexAskForApprovalGranular ToGranular() =>
        new()
        {
            McpElicitations = !McpElicitations,
            Rules = !Rules,
            SandboxApproval = !SandboxApproval,
            SkillApproval = true,
            RequestPermissions = true
        };
}
