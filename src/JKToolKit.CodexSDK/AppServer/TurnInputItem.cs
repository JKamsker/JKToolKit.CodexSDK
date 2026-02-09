using JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

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
    /// <summary>
    /// Gets the low-level wire payload object.
    /// </summary>
    public object Wire { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TurnInputItem"/> from a wire payload.
    /// </summary>
    public TurnInputItem(object wire)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
    }

    /// <summary>
    /// Creates a text input item.
    /// </summary>
    public static TurnInputItem Text(string text) =>
        new(TextUserInput.Create(text));

    /// <summary>
    /// Creates an image input item referencing a URL.
    /// </summary>
    public static TurnInputItem ImageUrl(string url) =>
        new(new ImageUserInput { Url = url });

    /// <summary>
    /// Creates an image input item referencing a local file path.
    /// </summary>
    public static TurnInputItem LocalImage(string path) =>
        new(new LocalImageUserInput { Path = path });

    /// <summary>
    /// Creates a skill input item.
    /// </summary>
    public static TurnInputItem Skill(string name, string path) =>
        new(new SkillUserInput { Name = name, Path = path });

    /// <summary>
    /// Creates a mention input item.
    /// </summary>
    public static TurnInputItem Mention(string name, string path) =>
        new(new MentionUserInput { Name = name, Path = path });
}
