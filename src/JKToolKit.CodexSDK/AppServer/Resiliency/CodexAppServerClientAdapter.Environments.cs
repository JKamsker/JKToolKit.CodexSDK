namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<EnvironmentAddResult> AddEnvironmentAsync(EnvironmentAddOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<EnvironmentAddResult> AddEnvironmentAsync(EnvironmentAddOptions options, CancellationToken ct) =>
        _inner.AddEnvironmentAsync(options, ct);
}
