using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents a user message item.
/// </summary>
public sealed record class CodexThreadItemUserMessage(
    string Id,
    string Type,
    IReadOnlyList<JsonElement> ContentItems,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a hook prompt item.
/// </summary>
public sealed record class CodexThreadItemHookPrompt(
    string Id,
    string Type,
    IReadOnlyList<CodexHookPromptFragment> Fragments,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a single hook prompt fragment.
/// </summary>
public sealed record class CodexHookPromptFragment(string Text, string HookRunId);

/// <summary>
/// Represents an agent message item.
/// </summary>
public sealed record class CodexThreadItemAgentMessage(
    string Id,
    string Type,
    string Text,
    string? Phase,
    JsonElement? MemoryCitation,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a proposed plan item.
/// </summary>
public sealed record class CodexThreadItemPlan(
    string Id,
    string Type,
    string Text,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a reasoning-summary item produced by the agent.
/// </summary>
public sealed record class CodexThreadItemReasoning(
    string Id,
    string Type,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string>? Content,
    string? EncryptedContent,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an item emitted when the thread history was compacted.
/// </summary>
public sealed record class CodexThreadItemContextCompaction(
    string Id,
    string Type,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);
