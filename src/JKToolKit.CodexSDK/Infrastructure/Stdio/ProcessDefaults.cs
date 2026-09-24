namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal static class ProcessDefaults
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
}
