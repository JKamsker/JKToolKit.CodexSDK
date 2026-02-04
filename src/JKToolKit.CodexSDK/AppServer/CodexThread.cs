using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

public sealed record class CodexThread
{
    public string Id { get; }
    public JsonElement Raw { get; }

    public CodexThread(string id, JsonElement raw)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Raw = raw;
    }
}

