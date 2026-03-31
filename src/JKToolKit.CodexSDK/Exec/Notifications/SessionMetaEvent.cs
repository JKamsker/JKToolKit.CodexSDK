namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Exec.Protocol;
using System.Text.Json;

/// <summary>
/// Represents a session metadata event containing session configuration and context.
/// </summary>
/// <remarks>
/// This event is typically emitted at the start of a session and contains
/// information about the session identifier and working directory.
/// </remarks>
public record SessionMetaEvent : CodexEvent
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public required SessionId SessionId { get; init; }

    /// <summary>
    /// Gets the current working directory for the session.
    /// </summary>
    /// <remarks>
    /// May be null if the working directory information is not available.
    /// </remarks>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the Codex CLI version when provided.
    /// </summary>
    public string? CliVersion { get; init; }

    /// <summary>
    /// Gets the originator identifier when provided (e.g. <c>codex_cli_rs</c>).
    /// </summary>
    public string? Originator { get; init; }

    /// <summary>
    /// Gets the emitted source when provided (e.g. <c>cli</c>).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the structured source payload when provided.
    /// </summary>
    public JsonElement? SourceJson { get; init; }

    /// <summary>
    /// Gets the subagent name when this session was emitted by a subagent source (e.g. <c>review</c>).
    /// </summary>
    public string? SourceSubagent { get; init; }

    /// <summary>
    /// Gets the parent/forked session id when provided.
    /// </summary>
    public SessionId? ForkedFromSessionId { get; init; }

    /// <summary>
    /// Gets the optional agent nickname assigned to this session.
    /// </summary>
    public string? AgentNickname { get; init; }

    /// <summary>
    /// Gets the optional agent role assigned to this session.
    /// </summary>
    public string? AgentRole { get; init; }

    /// <summary>
    /// Gets the optional canonical agent path assigned to this session.
    /// </summary>
    public string? AgentPath { get; init; }

    /// <summary>
    /// Gets the model provider when provided.
    /// </summary>
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets the optional base instructions payload when provided.
    /// </summary>
    public JsonElement? BaseInstructions { get; init; }

    /// <summary>
    /// Gets the optional dynamic tools payload when provided.
    /// </summary>
    public JsonElement? DynamicTools { get; init; }

    /// <summary>
    /// Gets the optional git metadata payload when provided.
    /// </summary>
    public JsonElement? Git { get; init; }

    /// <summary>
    /// Gets the optional memory mode when provided.
    /// </summary>
    public string? MemoryMode { get; init; }
}
