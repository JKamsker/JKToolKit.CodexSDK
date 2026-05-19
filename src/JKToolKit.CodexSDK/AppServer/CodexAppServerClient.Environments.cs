namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Registers an execution environment with the app-server.
    /// </summary>
    public Task<EnvironmentAddResult> AddEnvironmentAsync(EnvironmentAddOptions options, CancellationToken ct = default) =>
        _environmentsClient.AddAsync(options, ct);
}
