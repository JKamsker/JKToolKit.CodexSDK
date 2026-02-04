using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

public sealed class ThreadResumeOptions
{
    public required string ThreadId { get; set; }

    /// <summary>
    /// [UNSTABLE] If specified, resume using the provided history instead of loading from disk.
    /// </summary>
    public JsonElement? History { get; set; }

    /// <summary>
    /// [UNSTABLE] If specified, resume from a specific rollout path (takes precedence over <see cref="ThreadId"/>).
    /// </summary>
    public string? Path { get; set; }

    public CodexModel? Model { get; set; }
    public string? ModelProvider { get; set; }
    public string? Cwd { get; set; }
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Optional config overrides (arbitrary JSON object).
    /// </summary>
    public JsonElement? Config { get; set; }

    public string? BaseInstructions { get; set; }
    public string? DeveloperInstructions { get; set; }
    public string? Personality { get; set; }
}

