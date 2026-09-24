using System.Collections.Generic;
using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadTurnParsers
{
    public static IReadOnlyList<CodexTurn>? ParseTurns(JsonElement envelope)
    {
        var thread = TryGetObject(envelope, JsonFieldNames.Thread) ?? (envelope.ValueKind == JsonValueKind.Object ? envelope : (JsonElement?)null);
        return thread is { } threadObject ? ParseTurnsFromThread(threadObject) : null;
    }

    public static IReadOnlyList<CodexTurn>? ParseTurns(JsonElement primary, JsonElement secondary)
    {
        if (primary.ValueKind == JsonValueKind.Object &&
            TryGetArray(primary, JsonFieldNames.Turns) is not null)
        {
            return ParseTurnsFromThread(primary);
        }

        if (secondary.ValueKind == JsonValueKind.Object &&
            TryGetArray(secondary, JsonFieldNames.Turns) is not null)
        {
            return ParseTurnsFromThread(secondary);
        }

        return null;
    }

    private static IReadOnlyList<CodexTurn>? ParseTurnsFromThread(JsonElement thread)
    {
        var turns = TryGetArray(thread, JsonFieldNames.Turns);
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
