using System.Collections.Generic;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadTurnParsers
{
    public static IReadOnlyList<CodexTurn>? ParseTurns(JsonElement envelope)
    {
        var thread = TryGetObject(envelope, "thread") ?? (envelope.ValueKind == JsonValueKind.Object ? envelope : (JsonElement?)null);
        return thread is { } threadObject ? ParseTurnsFromThread(threadObject) : null;
    }

    public static IReadOnlyList<CodexTurn>? ParseTurns(JsonElement primary, JsonElement secondary)
    {
        if (primary.ValueKind == JsonValueKind.Object &&
            TryGetArray(primary, "turns") is not null)
        {
            return ParseTurnsFromThread(primary);
        }

        if (secondary.ValueKind == JsonValueKind.Object &&
            TryGetArray(secondary, "turns") is not null)
        {
            return ParseTurnsFromThread(secondary);
        }

        return null;
    }

    private static IReadOnlyList<CodexTurn>? ParseTurnsFromThread(JsonElement thread)
    {
        var turns = TryGetArray(thread, "turns");
        if (turns is null)
        {
            return null;
        }

        var parsed = new List<CodexTurn>();
        foreach (var turn in turns.Value.EnumerateArray())
        {
            var parsedTurn = CodexTurn.TryParse(turn);
            if (parsedTurn is not null)
            {
                parsed.Add(parsedTurn);
            }
        }

        return parsed;
    }
}
