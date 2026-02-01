namespace JKToolKit.CodexSDK.AppServer.Demo.Demos;

public interface IAppServerDemo
{
    Task RunAsync(string repoPath, CancellationToken ct);
}

