namespace NCodexSDK.AppServer;

public sealed record TurnInputItem(string Type, object Content)
{
    public static TurnInputItem Text(string text) =>
        new("text", new { text });
}

