namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a single input item for <c>turn/start</c>.
/// </summary>
/// <remarks>
/// The app-server wire format varies by item type. This type intentionally keeps a low-level
/// "wire payload" object for forward compatibility.
/// </remarks>
public sealed record TurnInputItem(object Wire)
{
    public static TurnInputItem Text(string text) =>
        new(new { type = "text", text });
}
