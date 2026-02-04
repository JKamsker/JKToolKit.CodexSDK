namespace JKToolKit.CodexSDK.AppServer;

using JKToolKit.CodexSDK.AppServer.Protocol;

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
        new(TextUserInput.Create(text));

    public static TurnInputItem ImageUrl(string url) =>
        new(new ImageUserInput(url));

    public static TurnInputItem LocalImage(string path) =>
        new(new LocalImageUserInput(path));

    public static TurnInputItem Skill(string name, string path) =>
        new(new SkillUserInput(name, path));

    public static TurnInputItem Mention(string name, string path) =>
        new(new MentionUserInput(name, path));
}
