using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexTurnCollector
{
    private readonly List<JsonElement> _items = [];
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    private JsonElement? _usage;
    private string? _diff;
    public bool IsPartial { get; set; }

    public void Observe(AppServerNotification notification)
    {
        switch (notification)
        {
            case ItemCompletedNotification item: Add(item.Item); break;
            case ThreadTokenUsageUpdatedNotification usage: _usage = usage.TokenUsage.Clone(); break;
            case TurnDiffUpdatedNotification diff: _diff = diff.Diff; break;
        }
    }

    private void Add(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return;
        var copy = item.Clone();
        var id = String(item, "id");
        if (id is not null && _indices.TryGetValue(id, out var index)) _items[index] = copy;
        else
        {
            if (id is not null) _indices[id] = _items.Count;
            _items.Add(copy);
        }
    }

    public CodexTurnResult Complete(string threadId, string turnId, TurnCompletedNotification completed)
    {
        if (completed.Turn.ValueKind == JsonValueKind.Object &&
            completed.Turn.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray()) Add(item);

        var messages = _items.Where(i => String(i, "type") == "agentMessage").ToArray();
        var final = messages.LastOrDefault(i => String(i, "phase") == "final_answer");
        if (final.ValueKind == JsonValueKind.Undefined)
            final = messages.LastOrDefault(i => String(i, "phase") is null);
        return new CodexTurnResult
        {
            ThreadId = threadId, TurnId = turnId, Status = completed.Status,
            Error = completed.Error?.Clone(), TerminalTurn = completed.Turn.Clone(),
            FinalResponse = String(final, "text") ?? string.Empty,
            Items = Array.AsReadOnly(_items.Select(CodexThreadItemParser.Parse).ToArray()),
            Usage = _usage is { } usage ? CodexTurnUsage.Parse(usage) : null, Diff = _diff, IsPartial = IsPartial
        };
    }

    private static string? String(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;
}
