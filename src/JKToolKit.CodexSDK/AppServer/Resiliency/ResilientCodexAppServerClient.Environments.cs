#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<EnvironmentAddResult> AddEnvironmentAsync(EnvironmentAddOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Environment, (c, token) => c.AddEnvironmentAsync(options, token), ct);
}

#pragma warning restore CS1591
