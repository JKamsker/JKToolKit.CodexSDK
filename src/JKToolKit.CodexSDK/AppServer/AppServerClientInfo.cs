namespace JKToolKit.CodexSDK.AppServer;

public sealed record class AppServerClientInfo
{
    public string Name { get; init; }
    public string Title { get; init; }
    public string Version { get; init; }

    public AppServerClientInfo(string name, string title, string version)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }
}

