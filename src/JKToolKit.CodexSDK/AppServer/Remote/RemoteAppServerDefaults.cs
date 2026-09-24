namespace JKToolKit.CodexSDK.AppServer.Remote;

internal static class RemoteAppServerDefaults
{
    public const int ContainerPort = 4500;
    public static readonly TimeSpan StartupPollInterval = TimeSpan.FromMilliseconds(200);
}
