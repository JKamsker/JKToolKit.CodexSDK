using System.Collections.Generic;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadTurnParsers
{
    public static IReadOnlyList<CodexTurn>? ParseTurns(JsonElement envelope)
    {
        var thread = TryGetObject(envelope, "thread");
        if (thread is null)
        {
            return null;
        }

        var turns = TryGetArray(thread.Value, "turns");
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

        return parsed.Count == 0 ? null : parsed;
    }
}
