using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a typed thread lifecycle response envelope returned by the Codex app server.
/// </summary>
public sealed record class CodexThread
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the parsed thread snapshot.
    /// </summary>
    public CodexThreadSummary Thread { get; }

    /// <summary>
    /// Gets the approval policy returned by the lifecycle response, when present.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; }

    /// <summary>
    /// Gets the raw approval policy payload returned by the lifecycle response, when present.
    /// </summary>
    public JsonElement? ApprovalPolicyRaw { get; }

    /// <summary>
    /// Gets the approval reviewer returned by the lifecycle response, when present.
    /// </summary>
    public CodexApprovalsReviewer? ApprovalsReviewer { get; }

    /// <summary>
    /// Gets the sandbox mode returned by the lifecycle response, when present.
    /// </summary>
    public CodexSandboxMode? Sandbox { get; }

    /// <summary>
    /// Gets the raw sandbox payload returned by the lifecycle response, when present.
    /// </summary>
    public JsonElement? SandboxRaw { get; }

    /// <summary>
    /// Gets the reasoning effort requested by the lifecycle response, when present.
    /// </summary>
    public CodexReasoningEffort? ReasoningEffort { get; }

    /// <summary>
    /// Gets the service tier returned by the lifecycle response, when present.
    /// </summary>
    public CodexServiceTier? ServiceTier { get; }

    /// <summary>
    /// Gets the raw JSON payload for the lifecycle response.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CodexThread"/>.
    /// </summary>
    public CodexThread(
        string id,
        JsonElement raw,
        CodexThreadSummary? thread = null,
        CodexApprovalPolicy? approvalPolicy = null,
        JsonElement? approvalPolicyRaw = null,
        CodexApprovalsReviewer? approvalsReviewer = null,
        CodexSandboxMode? sandbox = null,
        JsonElement? sandboxRaw = null,
        CodexServiceTier? serviceTier = null,
        CodexReasoningEffort? reasoningEffort = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Thread = thread ?? new CodexThreadSummary
        {
            ThreadId = id,
            Raw = raw
        };
        ApprovalPolicy = approvalPolicy;
        ApprovalPolicyRaw = approvalPolicyRaw;
        ApprovalsReviewer = approvalsReviewer;
        Sandbox = sandbox;
        SandboxRaw = sandboxRaw;
        ReasoningEffort = reasoningEffort;
        ServiceTier = serviceTier;
        Raw = raw;
    }
}

