namespace JKToolKit.CodexSDK.AppServer;

using JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Represents a single input item for <c>turn/start</c>.
/// </summary>
/// <remarks>
/// The app-server wire format varies by item type. This type intentionally keeps a low-level
/// "wire payload" object for forward compatibility.
/// </remarks>
public sealed record class TurnInputItem
{
    public object Wire { get; }

    public TurnInputItem(object wire)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
    }

    public static TurnInputItem Text(string text) =>
        new(TextUserInput.Create(text));

    public static TurnInputItem ImageUrl(string url) =>
        new(new ImageUserInput { Url = url });

    public static TurnInputItem LocalImage(string path) =>
        new(new LocalImageUserInput { Path = path });

    public static TurnInputItem Skill(string name, string path) =>
        new(new SkillUserInput { Name = name, Path = path });

    public static TurnInputItem Mention(string name, string path) =>
        new(new MentionUserInput { Name = name, Path = path });
}
