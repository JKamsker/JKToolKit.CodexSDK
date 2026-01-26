using NCodexSDK.Public.Models;

namespace NCodexSDK.AppServer;

public sealed class TurnStartOptions
{
    public IReadOnlyList<TurnInputItem> Input { get; set; } = Array.Empty<TurnInputItem>();

    public CodexModel? Model { get; set; }
    public CodexReasoningEffort? Effort { get; set; }
    public string? Cwd { get; set; }
}

